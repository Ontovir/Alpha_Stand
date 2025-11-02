using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc;
using LoanService.Models;
using LoanService.Repositories;
using LoanService.Services;

namespace LoanService.Controllers;

/// <summary>
/// API для работы с займами
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{
    private readonly ILoanRepository _repository;
    private readonly IKafkaProducerService _kafka;
    private readonly ILogger<LoansController> _logger;
    private readonly Counter<long> _loansCreatedCounter;

    public LoansController(
        ILoanRepository repository,
        IKafkaProducerService kafka,
        ILogger<LoansController> logger,
        IMeterFactory meterFactory)
    {
        _repository = repository;
        _kafka = kafka;
        _logger = logger;

        var meter = meterFactory.Create("LoanService.Api");
        _loansCreatedCounter = meter.CreateCounter<long>("loans_created_total", description: "Total loans created");
    }

    /// <summary>
    /// Создать новый займ
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LoanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLoan([FromBody] CreateLoanRequest request, CancellationToken ct)
    {
        // Guard clauses
        if (request.Amount <= 0)
            return BadRequest(new { Error = "Amount must be positive" });

        try
        {
            // Сохранение в БД
            var loan = await _repository.CreateAsync(request, ct);

            // Отправка события в Kafka
            var @event = new LoanCreatedEvent
            {
                LoanId = loan.Id,
                UserId = loan.UserId,
                Amount = loan.Amount,
                CreatedAt = loan.CreatedAt
            };

            await _kafka.PublishLoanCreatedAsync(@event, ct);

            // Метрика
            _loansCreatedCounter.Add(1, new KeyValuePair<string, object?>("status", "success"));

            return CreatedAtAction(nameof(CreateLoan), new { id = loan.Id }, loan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create loan for user {UserId}", request.UserId);
            _loansCreatedCounter.Add(1, new KeyValuePair<string, object?>("status", "error"));
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}