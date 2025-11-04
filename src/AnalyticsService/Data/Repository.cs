using System.Data;
using Dapper;
using AnalyticsService.Models;
using Npgsql;

namespace AnalyticsService.Data;

/// <summary>
/// Репозиторий для сохранения аналитики
/// </summary>
public interface IAnalyticsRepository
{
    Task<bool> SaveAsync(LoanCreatedEvent @event, CancellationToken ct);
}

public sealed class AnalyticsRepository : IAnalyticsRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<AnalyticsRepository> _logger;

    public AnalyticsRepository(NpgsqlDataSource dataSource, ILogger<AnalyticsRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<bool> SaveAsync(LoanCreatedEvent @event, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO analytics_loans (loan_id, processed_at)
            VALUES (@LoanId, NOW())
            ON CONFLICT (loan_id) DO NOTHING
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, @event, cancellationToken: ct));

        if (rowsAffected == 0)
        {
            _logger.LogWarning("Duplicate loan event skipped: {LoanId}", @event.LoanId);
            return false; // Дубликат
        }

        _logger.LogInformation("Analytics saved for loan {LoanId}", @event.LoanId);
        return true; // Успешно сохранено
    }
}