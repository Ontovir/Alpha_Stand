namespace ApiTests.Models;

public sealed record CreateLoanRequest
{
    public required int UserId { get; init; }
    public required decimal Amount { get; init; }
}