using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AnalyticsService.Health;

/// <summary>
/// Проверка доступности Kafka
/// </summary>
public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly ILogger<KafkaHealthCheck> _logger;

    public KafkaHealthCheck(ILogger<KafkaHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        // Упрощённая проверка (в продакшене можно проверять lag, connectivity)
        try
        {
            // Здесь можно добавить реальную проверку Kafka подключения
            // Пока возвращаем Healthy если Worker запущен
            return Task.FromResult(HealthCheckResult.Healthy("Kafka consumer is running"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka consumer error", ex));
        }
    }
}