using System.Data;
using Dapper;
using Npgsql;
using RestSharp;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Allure.NUnit.Attributes;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace ApiTests.Infrastructure;

/// <summary>
/// Базовый класс для интеграционных тестов с Testcontainers
/// </summary>
[AllureParentSuite("Integration Tests")]
public abstract class IntegrationTestBase
{
    protected PostgreSqlContainer PostgresContainer = null!;
    protected KafkaContainer KafkaContainer = null!;
    protected RestClient ApiClient = null!;
    protected string ConnectionString = null!;

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // PostgreSQL Container
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("loan_db")
            .WithUsername("loan_user")
            .WithPassword("loan_password")
            .Build();

        await PostgresContainer.StartAsync();
        ConnectionString = PostgresContainer.GetConnectionString();

        // Инициализация БД (схема из init.sql)
        await InitializeDatabaseAsync();

        // Kafka Container
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

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        await PostgresContainer.DisposeAsync();
        await KafkaContainer.DisposeAsync();
        ApiClient?.Dispose();
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Создание таблиц (упрощённая версия init.sql)
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS loans (
                id SERIAL PRIMARY KEY,
                user_id INTEGER NOT NULL REFERENCES users(id),
                amount NUMERIC(15, 2) NOT NULL CHECK (amount > 0),
                status TEXT NOT NULL DEFAULT 'pending',
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS analytics_loans (
                id SERIAL PRIMARY KEY,
                loan_id INTEGER NOT NULL UNIQUE,
                processed_at TIMESTAMPTZ DEFAULT NOW()
            );

            -- Тестовые пользователи
            INSERT INTO users (name, email) VALUES 
                ('Test User 1', 'test1@example.com'),
                ('Test User 2', 'test2@example.com')
            ON CONFLICT (email) DO NOTHING;
            """);
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