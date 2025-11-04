using ApiTests.Infrastructure;
using ApiTests.Models;
using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using FluentAssertions;
using RestSharp;

namespace ApiTests.Tests;

[AllureNUnit]
[AllureSuite("LoanService API")]
[AllureFeature("Loan Creation")]
public class LoanServiceTests : IntegrationTestBase
{
    [Test]
    [AllureTag("api", "smoke")]
    [AllureSeverity(SeverityLevel.critical)]
    [AllureDescription("Проверка создания займа через POST /api/loans")]
    public async Task CreateLoan_ValidRequest_Returns201AndSavesToDatabase()
    {
        // Arrange
        var request = new CreateLoanRequest
        {
            UserId = 1,
            Amount = 10000.50m
        };

        var restRequest = new RestRequest("/api/loans", Method.Post)
            .AddJsonBody(request);

        // Act
        var response = await ApiClient.ExecuteAsync<LoanResponse>(restRequest);

        // Assert
        AllureApi.Step("Проверка HTTP статуса 201", () =>
        {
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
            response.Data.Should().NotBeNull();
        });

        var loan = response.Data!;

        AllureApi.Step("Проверка полей ответа", () =>
        {
            loan.Id.Should().BeGreaterThan(0);
            loan.UserId.Should().Be(request.UserId);
            loan.Amount.Should().Be(request.Amount);
            loan.Status.Should().Be("pending");
            loan.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });

        await AllureApi.Step("Проверка сохранения в БД", async () =>
        {
            var dbLoan = await QuerySingleOrDefaultAsync<LoanResponse>(
                "SELECT id, user_id AS UserId, amount, status, created_at AS CreatedAt FROM loans WHERE id = @Id",
                new { loan.Id });

            dbLoan.Should().NotBeNull();
            dbLoan!.UserId.Should().Be(request.UserId);
            dbLoan.Amount.Should().Be(request.Amount);
        });
    }

    [Test]
    [AllureTag("api", "validation")]
    [AllureSeverity(SeverityLevel.normal)]
    [AllureDescription("Проверка валидации: отрицательная сумма займа")]
    public async Task CreateLoan_NegativeAmount_Returns400()
    {
        // Arrange
        var request = new CreateLoanRequest
        {
            UserId = 1,
            Amount = -1000m
        };

        var restRequest = new RestRequest("/api/loans", Method.Post)
            .AddJsonBody(request);

        // Act
        var response = await ApiClient.ExecuteAsync(restRequest);

        // Assert
        AllureApi.Step("Проверка HTTP статуса 400", () =>
        {
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        });
    }

    [Test]
    [AllureTag("api", "validation")]
    [AllureSeverity(SeverityLevel.normal)]
    [AllureDescription("Проверка валидации: нулевая сумма займа")]
    public async Task CreateLoan_ZeroAmount_Returns400()
    {
        // Arrange
        var request = new CreateLoanRequest
        {
            UserId = 1,
            Amount = 0
        };

        var restRequest = new RestRequest("/api/loans", Method.Post)
            .AddJsonBody(request);

        // Act
        var response = await ApiClient.ExecuteAsync(restRequest);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}