using LoanService.Repositories;
using LoanService.Services;
using Npgsql;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("PostgreSQL connection string not found");

builder.Services.AddNpgsqlDataSource(connectionString);

// Services
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql");

Log.Information("Building application...");

var app = builder.Build();

Log.Information("Application built successfully");

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

// Prometheus metrics endpoint
app.MapMetrics();

// Health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

Log.Information("Middleware configured");

try
{
    Log.Information("Starting LoanService on port {Port}", builder.Configuration["LOAN_SERVICE_PORT"] ?? "5001");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}