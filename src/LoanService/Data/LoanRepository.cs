using System.Data;
using Dapper;
using LoanService.Models;
using Npgsql;

namespace LoanService.Repositories;

/// <summary>
/// Репозиторий для работы с займами
/// </summary>
public interface ILoanRepository
{
    Task<LoanResponse> CreateAsync(CreateLoanRequest request, CancellationToken ct);
}

public sealed class LoanRepository : ILoanRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<LoanRepository> _logger;

    public LoanRepository(NpgsqlDataSource dataSource, ILogger<LoanRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<LoanResponse> CreateAsync(CreateLoanRequest request, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO loans (user_id, amount, status)
            VALUES (@UserId, @Amount, 'pending')
            RETURNING id, user_id AS UserId, amount, status, created_at AS CreatedAt
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        var loan = await connection.QuerySingleAsync<LoanResponse>(
            new CommandDefinition(sql, request, cancellationToken: ct));

        _logger.LogInformation("Loan created: {LoanId} for user {UserId}, amount {Amount}",
            loan.Id, loan.UserId, loan.Amount);

        return loan;
    }
}