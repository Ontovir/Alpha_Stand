using System.Text.Json;
using ApiTests.Infrastructure;
using ApiTests.Models;
using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using Confluent.Kafka;
using FluentAssertions;
using RestSharp;

namespace ApiTests.Tests;

[AllureNUnit]
[AllureSuite("End-to-End")]
[AllureFeature("Loan Processing Pipeline")]
public class EndToEndTests : IntegrationTestBase
{
    [Test]
    [Ignore("Requires docker-compose for full E2E")]
    [AllureTag("e2e", "kafka", "critical")]
    [AllureSeverity(SeverityLevel.blocker)]
    [AllureDescription("Полная проверка: POST /loans → Kafka event → Analytics DB")]
    public async Task CreateLoan_EventProcessedByAnalytics_SavedToAnalyticsTable()
    {
        // Arrange
        var request = new CreateLoanRequest
        {
            UserId = 1,
            Amount = 25000m
        };

        // Act - Создание займа
        var loanId = await AllureApi.Step("Создание займа через API", async () =>
        {
            var restRequest = new RestRequest("/api/loans", Method.Post)
                .AddJsonBody(request);

            var response = await ApiClient.ExecuteAsync<LoanResponse>(restRequest);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
            response.Data.Should().NotBeNull();

            return response.Data!.Id;
        });

        // Assert - Проверка события в Kafka
        await AllureApi.Step("Проверка события в Kafka topic", async () =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = KafkaContainer.GetBootstrapAddress(),
                GroupId = "test-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            consumer.Subscribe("loan_events");

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var eventFound = false;

            while (!cts.Token.IsCancellationRequested && !eventFound)
            {
                var result = consumer.Consume(cts.Token);
                if (result != null)
                {
                    var @event = JsonSerializer.Deserialize<LoanCreatedEvent>(result.Message.Value);
                    if (@event?.LoanId == loanId)
                    {
                        @event.UserId.Should().Be(request.UserId);
                        @event.Amount.Should().Be(request.Amount);
                        eventFound = true;
                    }
                }
            }

            eventFound.Should().BeTrue("Event should be published to Kafka");
            await Task.CompletedTask;
        });

        // Assert - Ждём обработки AnalyticsService (retry + async)
        await AllureApi.Step("Ожидание обработки AnalyticsService (до 30 сек)", async () =>
        {
            var retries = 30;
            var found = false;

            while (retries > 0 && !found)
            {
                var analyticsRecord = await QuerySingleOrDefaultAsync<int?>(
                    "SELECT loan_id FROM analytics_loans WHERE loan_id = @LoanId",
                    new { LoanId = loanId });

                if (analyticsRecord.HasValue)
                {
                    found = true;
                    break;
                }

                await Task.Delay(1000);
                retries--;
            }

            found.Should().BeTrue($"Loan {loanId} should be processed by AnalyticsService within 30 seconds");
        });
    }
}

public sealed record LoanCreatedEvent
{
    public int LoanId { get; init; }
    public int UserId { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; }
}