using System.Diagnostics.Metrics;
using System.Text.Json;
using AnalyticsService.Data;
using AnalyticsService.Models;
using Confluent.Kafka;

namespace AnalyticsService.Workers;

/// <summary>
/// Фоновый Worker для обработки событий из Kafka
/// </summary>
public sealed class KafkaConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaConsumerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _dlqProducer;
    private readonly Counter<long> _eventsProcessedCounter;
    private readonly Counter<long> _eventsFailedCounter;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;

    public KafkaConsumerWorker(
        ILogger<KafkaConsumerWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration config,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config;

        _maxRetries = config.GetValue<int>("Kafka:MaxRetries");
        _retryDelayMs = config.GetValue<int>("Kafka:RetryDelayMs");

        // Kafka Consumer
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            GroupId = config["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false // Ручной commit после успешной обработки
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        _consumer.Subscribe(config["Kafka:Topic"]);

        // DLQ Producer
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _dlqProducer = new ProducerBuilder<string, string>(producerConfig).Build();

        // Метрики
        var meter = meterFactory.Create("AnalyticsService.Consumer");
        _eventsProcessedCounter = meter.CreateCounter<long>("events_processed_total", description: "Total events processed");
        _eventsFailedCounter = meter.CreateCounter<long>("events_failed_total", description: "Total events failed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaConsumerWorker started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult != null)
                    {
                        await ProcessMessageAsync(consumeResult, stoppingToken);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error");
                }
            }
        }
        finally
        {
            _consumer.Close();
            _dlqProducer.Dispose();
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var attempts = 0;

        while (attempts < _maxRetries)
        {
            attempts++;

            try
            {
                // Десериализация события
                var @event = JsonSerializer.Deserialize<LoanCreatedEvent>(result.Message.Value)
                    ?? throw new InvalidOperationException("Failed to deserialize event");

                // Обработка через Repository (scoped service)
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAnalyticsRepository>();

                var saved = await repository.SaveAsync(@event, ct);

                // Commit offset только после успешной обработки
                _consumer.Commit(result);

                _eventsProcessedCounter.Add(1, new KeyValuePair<string, object?>("duplicate", (!saved).ToString()));

                _logger.LogInformation("Event processed: {LoanId}, attempt {Attempt}", @event.LoanId, attempts);
                return; // Успех
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Processing failed for loan {LoanId}, attempt {Attempt}/{MaxRetries}",
                    result.Message.Key, attempts, _maxRetries);

                if (attempts < _maxRetries)
                {
                    // Exponential backoff: 1s → 2s → 4s
                    var delay = _retryDelayMs * (int)Math.Pow(2, attempts - 1);
                    await Task.Delay(delay, ct);
                }
            }
        }

        // Все попытки исчерпаны → отправляем в DLQ
        await SendToDlqAsync(result, ct);
        _consumer.Commit(result); // Commit чтобы не блокировать очередь
        _eventsFailedCounter.Add(1);
    }

    private async Task SendToDlqAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var dlqTopic = _config["Kafka:DlqTopic"] ?? throw new InvalidOperationException("DLQ topic not configured");

        var message = new Message<string, string>
        {
            Key = result.Message.Key,
            Value = result.Message.Value,
            Headers = result.Message.Headers
        };

        await _dlqProducer.ProduceAsync(dlqTopic, message, ct);

        _logger.LogError("Event sent to DLQ: {LoanId} after {MaxRetries} attempts",
            result.Message.Key, _maxRetries);
    }
}