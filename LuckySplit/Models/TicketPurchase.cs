using System;

namespace LuckySplit.Models;

[Serializable]
public sealed class TicketPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientTransactionId { get; set; } = Guid.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int StartTicket { get; set; }
    public int EndTicket { get; set; }
    public DateTimeOffset PurchasedAt { get; set; } = DateTimeOffset.UtcNow;
    public string RecordedBy { get; set; } = "Host";
    public long RevisionNumber { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(World)
        ? PlayerName
        : $"{PlayerName} @ {World}";
}
