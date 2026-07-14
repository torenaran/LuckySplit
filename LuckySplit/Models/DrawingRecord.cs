using System;
using System.Collections.Generic;
using System.Linq;

namespace LuckySplit.Models;

[Serializable]
public sealed class DrawingRecord
{
    public Guid Id { get; set; } = Guid.Empty;
    public Guid PresetId { get; set; } = Guid.Empty;
    public string Title { get; set; } = "Tonight's 50/50 Drawing";
    public string VenueName { get; set; } = "Everbloom";
    public int TicketPrice { get; set; } = 100_000;
    public int WinnerPercent { get; set; } = 50;
    public int MaxTicketsPerPlayer { get; set; }
    public DrawingStatus Status { get; set; } = DrawingStatus.Draft;
    public string ProtocolVersion { get; set; } = "LSPLIT1";

    public string SeedHex { get; set; } = string.Empty;
    public string CommitmentHash { get; set; } = string.Empty;
    public string LedgerHash { get; set; } = string.Empty;
    public string ResultDigestHex { get; set; } = string.Empty;

    public List<TicketPurchase> Purchases { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? DrawnAt { get; set; }

    public int WinningTicket { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    public string WinnerWorld { get; set; } = string.Empty;
    public long WinnerPayout { get; set; }
    public long VenueShare { get; set; }

    public int TotalTickets => Purchases.Sum(p => p.Quantity);
    public long TotalPot => (long)TotalTickets * TicketPrice;
    public int UniquePlayers => Purchases
        .Select(p => $"{p.PlayerName.Trim().ToUpperInvariant()}|{p.World.Trim().ToUpperInvariant()}")
        .Distinct()
        .Count();

    public string WinnerDisplayName => string.IsNullOrWhiteSpace(WinnerWorld)
        ? WinnerName
        : $"{WinnerName} @ {WinnerWorld}";
}
