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
    public long StartingPrizeGil { get; set; }
    public DrawingStatus Status { get; set; } = DrawingStatus.Draft;
    public string ProtocolVersion { get; set; } = "LSPLIT3";

    // Collaboration-ready metadata. Local drawings leave CollaborationEnabled false.
    public bool CollaborationEnabled { get; set; }
    public string CollaborationSessionId { get; set; } = string.Empty;
    public long RevisionNumber { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.UtcNow;

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
    public long TicketRevenue => (long)TotalTickets * TicketPrice;

    public long TotalPrizePool => StartingPrizeGil + TicketRevenue;

    // Retained for compatibility with existing UI/history code.
    public long TotalPot => TotalPrizePool;

    public long ProjectedWinnerPayout
    {
        get
        {
            if (string.Equals(ProtocolVersion, "LSPLIT2", StringComparison.OrdinalIgnoreCase))
                return StartingPrizeGil + (TicketRevenue * WinnerPercent / 100);

            if (string.Equals(ProtocolVersion, "LSPLIT3", StringComparison.OrdinalIgnoreCase))
                return TotalPrizePool * WinnerPercent / 100;

            return TicketRevenue * WinnerPercent / 100;
        }
    }

    public long ProjectedVenueShare
    {
        get
        {
            if (string.Equals(ProtocolVersion, "LSPLIT3", StringComparison.OrdinalIgnoreCase))
                return TotalPrizePool - ProjectedWinnerPayout;

            var ticketWinnerShare = TicketRevenue * WinnerPercent / 100;
            return TicketRevenue - ticketWinnerShare;
        }
    }

    public long TotalGilCommitted => TotalPrizePool;

    public int UniquePlayers => Purchases
        .Select(p => $"{p.PlayerName.Trim().ToUpperInvariant()}|{p.World.Trim().ToUpperInvariant()}")
        .Distinct()
        .Count();

    public string WinnerDisplayName => string.IsNullOrWhiteSpace(WinnerWorld)
        ? WinnerName
        : $"{WinnerName} @ {WinnerWorld}";
}
