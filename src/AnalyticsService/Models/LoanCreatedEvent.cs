namespace AnalyticsService.Models;

/// <summary>
/// Событие создания займа из Kafka
/// </summary>
public sealed record LoanCreatedEvent
{
    public required int LoanId { get; init; }
    public required int UserId { get; init; }
    public required decimal Amount { get; init; }
    public required DateTime CreatedAt { get; init; }
}