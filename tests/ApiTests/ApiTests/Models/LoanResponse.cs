namespace ApiTests.Models;

public sealed record LoanResponse
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}