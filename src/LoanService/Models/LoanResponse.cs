namespace LoanService.Models;

/// <summary>
/// Ответ после создания займа
/// </summary>
public sealed record LoanResponse
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
    public required decimal Amount { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}