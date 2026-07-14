using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LuckySplit.Models;

namespace LuckySplit.Services;

public sealed class DrawingService
{
    public DrawingRecord CreateDraft(Configuration configuration)
    {
        var preset = configuration.GetSelectedPreset();
        if (preset is not null)
            return CreateDraft(preset);

        // Legacy fallback used only until the user completes first-run preset setup.
        return new DrawingRecord
        {
            Title = "Tonight's 50/50 Drawing",
            VenueName = configuration.DefaultVenueName,
            TicketPrice = configuration.DefaultTicketPrice,
            WinnerPercent = configuration.DefaultWinnerPercent,
            MaxTicketsPerPlayer = configuration.DefaultMaxTicketsPerPlayer,
            Status = DrawingStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public DrawingRecord CreateDraft(VenuePreset preset)
    {
        var drawing = new DrawingRecord
        {
            Status = DrawingStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ApplyPresetToDraft(drawing, preset);
        return drawing;
    }

    public bool ApplyPresetToDraft(DrawingRecord drawing, VenuePreset preset)
    {
        if (drawing.Status != DrawingStatus.Draft || drawing.Purchases.Count > 0)
            return false;

        drawing.PresetId = preset.Id;
        drawing.Title = preset.DefaultDrawingTitle;
        drawing.VenueName = preset.VenueName;
        drawing.TicketPrice = preset.TicketPrice;
        drawing.WinnerPercent = preset.WinnerPercent;
        drawing.MaxTicketsPerPlayer = preset.MaxTicketsPerPlayer;
        return true;
    }

    public string OpenDrawing(DrawingRecord drawing)
    {
        if (drawing.Status != DrawingStatus.Draft)
            return "Only a draft drawing can be opened.";
        if (string.IsNullOrWhiteSpace(drawing.Title))
            return "Enter a drawing name.";
        if (string.IsNullOrWhiteSpace(drawing.VenueName))
            return "Enter a venue name.";
        if (drawing.TicketPrice <= 0 || drawing.TicketPrice > DrawingServiceLimits.MaxTicketPrice)
            return $"Ticket price must be between 1 and {DrawingServiceLimits.MaxTicketPrice:N0} gil.";
        if (drawing.WinnerPercent is < 1 or > 100)
            return "Winner percentage must be between 1 and 100.";
        if (drawing.MaxTicketsPerPlayer < 0 || drawing.MaxTicketsPerPlayer > DrawingServiceLimits.MaxTicketsPerDrawing)
            return $"Maximum tickets per player must be between 0 and {DrawingServiceLimits.MaxTicketsPerDrawing:N0}.";

        drawing.Id = Guid.NewGuid();
        drawing.ProtocolVersion = "LSPLIT1";
        drawing.SeedHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        drawing.CommitmentHash = ComputeCommitment(drawing);
        drawing.Status = DrawingStatus.Open;
        drawing.OpenedAt = DateTimeOffset.UtcNow;
        return string.Empty;
    }

    public string AddPurchase(DrawingRecord drawing, string playerName, string world, int quantity)
    {
        if (drawing.Status != DrawingStatus.Open)
            return "Ticket sales are not open.";

        playerName = playerName.Trim();
        world = world.Trim();

        if (string.IsNullOrWhiteSpace(playerName))
            return "Enter the buyer's character name.";
        if (quantity <= 0)
            return "Ticket quantity must be greater than zero.";
        if (quantity > DrawingServiceLimits.MaxTicketsPerPurchase)
            return $"A single purchase cannot exceed {DrawingServiceLimits.MaxTicketsPerPurchase:N0} tickets.";

        var proposedTotal = (long)drawing.TotalTickets + quantity;
        if (proposedTotal > DrawingServiceLimits.MaxTicketsPerDrawing)
            return $"A drawing cannot exceed {DrawingServiceLimits.MaxTicketsPerDrawing:N0} total tickets.";

        if (drawing.MaxTicketsPerPlayer > 0)
        {
            var existing = drawing.Purchases
                .Where(p => string.Equals(p.PlayerName.Trim(), playerName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.World.Trim(), world, StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Quantity);

            if ((long)existing + quantity > drawing.MaxTicketsPerPlayer)
                return $"That purchase would exceed the {drawing.MaxTicketsPerPlayer:N0}-ticket player limit.";
        }

        drawing.Purchases.Add(new TicketPurchase
        {
            PlayerName = playerName,
            World = world,
            Quantity = quantity,
            PurchasedAt = DateTimeOffset.UtcNow,
        });

        RecalculateTicketRanges(drawing);
        return string.Empty;
    }

    public bool RemovePurchase(DrawingRecord drawing, Guid purchaseId)
    {
        if (drawing.Status != DrawingStatus.Open)
            return false;

        var purchase = drawing.Purchases.FirstOrDefault(p => p.Id == purchaseId);
        if (purchase is null)
            return false;

        drawing.Purchases.Remove(purchase);
        RecalculateTicketRanges(drawing);
        return true;
    }

    public string CloseSales(DrawingRecord drawing)
    {
        if (drawing.Status != DrawingStatus.Open)
            return "Ticket sales are not open.";
        if (drawing.TotalTickets <= 0)
            return "At least one ticket must be sold before closing sales.";

        RecalculateTicketRanges(drawing);
        drawing.Status = DrawingStatus.Closed;
        drawing.ClosedAt = DateTimeOffset.UtcNow;
        return string.Empty;
    }

    public string ReopenSales(DrawingRecord drawing)
    {
        if (drawing.Status != DrawingStatus.Closed)
            return "Only a closed, undrawn drawing can be reopened.";

        drawing.Status = DrawingStatus.Open;
        drawing.ClosedAt = null;
        return string.Empty;
    }

    public string DrawWinner(DrawingRecord drawing)
    {
        if (drawing.Status != DrawingStatus.Closed)
            return "Close ticket sales before drawing a winner.";
        if (drawing.TotalTickets <= 0)
            return "There are no tickets in this drawing.";
        if (string.IsNullOrWhiteSpace(drawing.SeedHex))
            return "The drawing seed is missing.";

        RecalculateTicketRanges(drawing);
        drawing.LedgerHash = ComputeLedgerHash(drawing);

        var material = $"{GetProtocolVersion(drawing)}|{drawing.Id:N}|{drawing.LedgerHash}|{drawing.TotalTickets}";
        using var hmac = new HMACSHA256(Convert.FromHexString(drawing.SeedHex));
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(material));
        drawing.ResultDigestHex = Convert.ToHexString(digest);

        var value = BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, sizeof(ulong)));
        drawing.WinningTicket = checked((int)(value % (ulong)drawing.TotalTickets) + 1);

        var winningPurchase = drawing.Purchases.First(p =>
            drawing.WinningTicket >= p.StartTicket && drawing.WinningTicket <= p.EndTicket);

        drawing.WinnerName = winningPurchase.PlayerName;
        drawing.WinnerWorld = winningPurchase.World;
        drawing.WinnerPayout = drawing.TotalPot * drawing.WinnerPercent / 100;
        drawing.VenueShare = drawing.TotalPot - drawing.WinnerPayout;
        drawing.Status = DrawingStatus.Drawn;
        drawing.DrawnAt = DateTimeOffset.UtcNow;
        return string.Empty;
    }

    public void VoidDrawing(DrawingRecord drawing)
    {
        if (drawing.Status == DrawingStatus.Drawn)
            return;

        drawing.Status = DrawingStatus.Voided;
        drawing.ClosedAt ??= DateTimeOffset.UtcNow;
    }

    public string ComputeCommitment(DrawingRecord drawing)
    {
        var canonical = string.Join('|',
            GetProtocolVersion(drawing),
            drawing.Id.ToString("N"),
            CanonicalText(drawing.VenueName),
            CanonicalText(drawing.Title),
            drawing.TicketPrice.ToString(CultureInfo.InvariantCulture),
            drawing.WinnerPercent.ToString(CultureInfo.InvariantCulture),
            drawing.MaxTicketsPerPlayer.ToString(CultureInfo.InvariantCulture),
            drawing.SeedHex.ToUpperInvariant());

        return Sha256Hex(canonical);
    }

    public bool VerifyCommitment(DrawingRecord drawing)
    {
        if (string.IsNullOrWhiteSpace(drawing.CommitmentHash) || string.IsNullOrWhiteSpace(drawing.SeedHex))
            return false;

        return string.Equals(
            drawing.CommitmentHash,
            ComputeCommitment(drawing),
            StringComparison.OrdinalIgnoreCase);
    }

    public string ComputeLedgerHash(DrawingRecord drawing)
    {
        var canonicalRows = drawing.Purchases
            .OrderBy(p => p.StartTicket)
            .Select(p => string.Join('|',
                p.StartTicket.ToString(CultureInfo.InvariantCulture),
                p.EndTicket.ToString(CultureInfo.InvariantCulture),
                CanonicalText(p.PlayerName),
                CanonicalText(p.World),
                p.Quantity.ToString(CultureInfo.InvariantCulture)));

        return Sha256Hex(string.Join(';', canonicalRows));
    }

    public string BuildOpeningAnnouncement(DrawingRecord drawing)
    {
        var limit = drawing.MaxTicketsPerPlayer > 0
            ? $" Limit: {drawing.MaxTicketsPerPlayer:N0} tickets per player."
            : string.Empty;

        return $"[{drawing.VenueName}] {drawing.Title} is now open! Tickets are {drawing.TicketPrice:N0} gil each. " +
               $"The winner receives {drawing.WinnerPercent}% of the final pot.{limit} Fairness commitment: {drawing.CommitmentHash}";
    }

    public string BuildSalesUpdate(DrawingRecord drawing)
    {
        var estimatedPayout = drawing.TotalPot * drawing.WinnerPercent / 100;
        return $"[{drawing.VenueName}] 50/50 update: {drawing.TotalTickets:N0} tickets sold to {drawing.UniquePlayers:N0} players. " +
               $"Current pot: {drawing.TotalPot:N0} gil. Estimated winner payout: {estimatedPayout:N0} gil.";
    }

    public string BuildWinnerAnnouncement(DrawingRecord drawing)
    {
        return $"[{drawing.VenueName}] Congratulations to {drawing.WinnerDisplayName}! Ticket #{drawing.WinningTicket:N0} wins " +
               $"{drawing.WinnerPayout:N0} gil from a {drawing.TotalPot:N0} gil pot. Thank you to everyone who entered!";
    }

    public string BuildVerificationReceipt(DrawingRecord drawing)
    {
        return string.Join(Environment.NewLine,
            $"Lucky Split verification receipt ({GetProtocolVersion(drawing)})",
            $"Drawing ID: {drawing.Id:N}",
            $"Venue: {drawing.VenueName}",
            $"Drawing: {drawing.Title}",
            $"Ticket price: {drawing.TicketPrice}",
            $"Winner percent: {drawing.WinnerPercent}",
            $"Total tickets: {drawing.TotalTickets}",
            $"Commitment: {drawing.CommitmentHash}",
            $"Revealed seed: {drawing.SeedHex}",
            $"Commitment valid: {VerifyCommitment(drawing)}",
            $"Ledger hash: {drawing.LedgerHash}",
            $"Result digest: {drawing.ResultDigestHex}",
            $"Winning ticket: {drawing.WinningTicket}",
            $"Winner: {drawing.WinnerDisplayName}",
            $"Winner payout: {drawing.WinnerPayout}",
            $"Venue share: {drawing.VenueShare}");
    }

    public string ExportCsv(DrawingRecord drawing, string directory)
    {
        Directory.CreateDirectory(directory);
        var safeTitle = string.Concat(drawing.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var fileName = $"LuckySplit_{drawing.CreatedAt:yyyyMMdd_HHmmss}_{safeTitle}.csv";
        var path = Path.Combine(directory, fileName);

        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Player,World,Quantity,StartTicket,EndTicket,TicketPrice,AmountPaid,PurchasedAtUtc");
        foreach (var purchase in drawing.Purchases.OrderBy(p => p.StartTicket))
        {
            writer.WriteLine(string.Join(',',
                Csv(purchase.PlayerName),
                Csv(purchase.World),
                purchase.Quantity.ToString(CultureInfo.InvariantCulture),
                purchase.StartTicket.ToString(CultureInfo.InvariantCulture),
                purchase.EndTicket.ToString(CultureInfo.InvariantCulture),
                drawing.TicketPrice.ToString(CultureInfo.InvariantCulture),
                ((long)purchase.Quantity * drawing.TicketPrice).ToString(CultureInfo.InvariantCulture),
                Csv(purchase.PurchasedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))));
        }

        return path;
    }

    public void RecalculateTicketRanges(DrawingRecord drawing)
    {
        long next = 1;
        foreach (var purchase in drawing.Purchases)
        {
            if (purchase.Quantity <= 0 || purchase.Quantity > DrawingServiceLimits.MaxTicketsPerPurchase)
                throw new InvalidOperationException("The ticket ledger contains an invalid purchase quantity.");

            var end = next + purchase.Quantity - 1L;
            if (end > DrawingServiceLimits.MaxTicketsPerDrawing)
                throw new InvalidOperationException($"The ticket ledger exceeds {DrawingServiceLimits.MaxTicketsPerDrawing:N0} tickets.");

            purchase.StartTicket = checked((int)next);
            purchase.EndTicket = checked((int)end);
            next = end + 1L;
        }
    }

    private static string GetProtocolVersion(DrawingRecord drawing)
    {
        return string.IsNullOrWhiteSpace(drawing.ProtocolVersion) ? "LSPLIT1" : drawing.ProtocolVersion;
    }

    private static string CanonicalText(string value) => value.Trim().Replace('|', '/').Replace(';', ',');

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
