using AnalyticsService.Data;
using AnalyticsService.Health;
using AnalyticsService.Workers;
using Npgsql;
using Prometheus;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

// PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("PostgreSQL connection string not found");

builder.Services.AddNpgsqlDataSource(connectionString);

// Repository
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

// Worker
builder.Services.AddHostedService<KafkaConsumerWorker>();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddCheck<KafkaHealthCheck>("kafka");

// Metrics server (для Prometheus scraping)
var metricsPort = builder.Configuration.GetValue<int>("ANALYTICS_SERVICE_PORT", 5002);
var metricServer = new Prometheus.KestrelMetricServer(port: metricsPort);
metricServer.Start();

var host = builder.Build();

try
{
    Log.Information("Starting AnalyticsService on port {Port}", metricsPort);
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
    metricServer.Stop();
}