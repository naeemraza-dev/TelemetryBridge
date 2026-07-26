using Microsoft.EntityFrameworkCore;
using Npgsql;
using TelemetryBridge.AspNetCore;
using TelemetryBridge.Persistence;
using TelemetryBridge.Sample.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-worker";
    options.ServiceVersion = "0.1.0";
    options.Environment = builder.Environment.EnvironmentName;
});

var databaseHost = builder.Configuration["Database:Host"]
    ?? throw new InvalidOperationException("Database:Host is required.");
var connection = new NpgsqlConnectionStringBuilder
{
    Host = databaseHost,
    Port = builder.Configuration.GetValue("Database:Port", 5432),
    Database = builder.Configuration["Database:Name"] ?? "telemetrybridge",
    Username = builder.Configuration["Database:Username"] ?? "telemetrybridge",
    Password = builder.Configuration["Database:Password"] ?? string.Empty
};
builder.Services.AddDbContextFactory<TelemetryBridgeDbContext>(options =>
    options.UseNpgsql(connection.ConnectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
builder.Services.AddHostedService<WorkItemProcessor>();

await builder.Build().RunAsync();
