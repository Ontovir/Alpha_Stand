namespace LoanService.Models;

/// <summary>
/// Запрос на создание займа
/// </summary>
public sealed record CreateLoanRequest
{
    /// <summary>
    /// ID пользователя
    /// </summary>
    public required int UserId { get; init; }

    /// <summary>
    /// Сумма займа
    /// </summary>
    public required decimal Amount { get; init; }
}