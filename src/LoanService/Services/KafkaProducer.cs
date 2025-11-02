using System.Text.Json;
using Confluent.Kafka;
using LoanService.Models;

namespace LoanService.Services;

/// <summary>
/// Сервис для отправки событий в Kafka
/// </summary>
public interface IKafkaProducerService
{
    Task PublishLoanCreatedAsync(LoanCreatedEvent @event, CancellationToken ct);
}

public sealed class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? throw new InvalidOperationException("Kafka:Topic not configured");

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All,
            MessageTimeoutMs = 5000,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishLoanCreatedAsync(LoanCreatedEvent @event, CancellationToken ct)
    {
        var key = @event.LoanId.ToString();
        var value = JsonSerializer.Serialize(@event);

        var message = new Message<string, string> { Key = key, Value = value };

        var result = await _producer.ProduceAsync(_topic, message, ct);

        _logger.LogInformation("Event published to Kafka: {Topic}, Partition {Partition}, Offset {Offset}",
            result.Topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer?.Dispose();
}