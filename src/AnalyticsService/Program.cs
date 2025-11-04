using AnalyticsService.Data;
using AnalyticsService.Health;
using AnalyticsService.Workers;
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

// Repository
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

// Kafka Consumer Worker
builder.Services.AddHostedService<KafkaConsumerWorker>();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddCheck<KafkaHealthCheck>("kafka");

var app = builder.Build();

// Middleware
app.UseSerilogRequestLogging();

// Endpoints
app.MapHealthChecks("/health");
app.MapMetrics();

try
{
    Log.Information("Starting AnalyticsService on port 5002 with KafkaConsumerWorker");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}