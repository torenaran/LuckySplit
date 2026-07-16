using System;

namespace LuckySplit.Models;

[Serializable]
public sealed class VenuePreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "My Venue";
    public string VenueName { get; set; } = "My Venue";
    public string DefaultDrawingTitle { get; set; } = "Tonight's 50/50 Drawing";
    public int TicketPrice { get; set; } = 100_000;
    public int WinnerPercent { get; set; } = 50;
    public int MaxTicketsPerPlayer { get; set; }
    public long StartingPrizeGil { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? VenueName : Name;
}
