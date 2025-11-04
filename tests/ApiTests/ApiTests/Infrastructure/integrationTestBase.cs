using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Dapper;
using Npgsql;
using RestSharp;
using Testcontainers.Kafka;
using Allure.NUnit.Attributes;
using Confluent.Kafka.Admin;
using NUnit.Framework; 

namespace ApiTests.Infrastructure;

/// <summary>
/// Базовый класс для интеграционных тестов
/// </summary>
[AllureParentSuite("Integration Tests")]
public abstract class IntegrationTestBase
{
    protected KafkaContainer KafkaContainer = null!;
    protected RestClient ApiClient = null!;
    protected string ConnectionString = null!;

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // ✅ Используем docker-compose PostgreSQL
        ConnectionString = "Host=localhost;Port=5432;Database=loan_db;Username=loan_user;Password=loan_password";

        // Kafka Container (для E2E тестов)
        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.8.0")
            .Build();

        await KafkaContainer.StartAsync();

        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = KafkaContainer.GetBootstrapAddress()
        };

        using var adminClient = new AdminClientBuilder(adminConfig).Build();
        await adminClient.CreateTopicsAsync(new[]
        {
            new TopicSpecification
            {
                Name = "loan_events",
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        });

        ApiClient = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("http://localhost:5001")
        });
    }

    [SetUp]
    public async Task TestSetup()
    {
        // Очистка данных перед каждым тестом
        await ExecuteAsync("TRUNCATE TABLE analytics_loans, loans RESTART IDENTITY CASCADE");
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        await KafkaContainer.DisposeAsync();
        ApiClient?.Dispose();
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
    }

    protected async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        return await connection.ExecuteAsync(sql, parameters);
    }
}