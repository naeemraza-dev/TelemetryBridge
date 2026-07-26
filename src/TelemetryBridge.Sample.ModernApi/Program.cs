using Microsoft.EntityFrameworkCore;
using Npgsql;
using TelemetryBridge.AspNetCore;
using TelemetryBridge.Core;
using TelemetryBridge.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTelemetryBridge(builder.Configuration, options =>
{
    options.ServiceName = "telemetrybridge-modern-api";
    options.ServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    options.Environment = builder.Environment.EnvironmentName;
});

var databaseHost = builder.Configuration["Database:Host"];
if (string.IsNullOrWhiteSpace(databaseHost))
{
    throw new InvalidOperationException("Database:Host is required.");
}

var connection = new NpgsqlConnectionStringBuilder
{
    Host = databaseHost,
    Port = builder.Configuration.GetValue("Database:Port", 5432),
    Database = builder.Configuration["Database:Name"] ?? "telemetrybridge",
    Username = builder.Configuration["Database:Username"] ?? "telemetrybridge",
    Password = builder.Configuration["Database:Password"] ?? string.Empty
};

builder.Services.AddDbContextPool<TelemetryBridgeDbContext>(options =>
    options.UseNpgsql(connection.ConnectionString, npgsql => npgsql.EnableRetryOnFailure(3)));
builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:InternalApi"] ?? "http://localhost:8082",
        UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(3);
}).AddStandardResilienceHandler();
builder.Services.AddHealthChecks().AddDbContextCheck<TelemetryBridgeDbContext>("postgresql");
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Correlation-ID")));

var app = builder.Build();

app.UseTelemetryBridge();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
    name = "TelemetryBridge Modern API",
    documentation = "/swagger",
    health = "/health"
})).ExcludeFromDescription();

app.MapGet("/api/orders", async (
    TelemetryBridgeDbContext database,
    CancellationToken cancellationToken) =>
{
    using var operation = TelemetryOperation.Start("order.list", "list");
    var orders = await database.Orders
        .AsNoTracking()
        .OrderByDescending(order => order.CreatedAt)
        .Take(20)
        .Select(order => new OrderResponse(order.Id, order.Channel, order.CreatedAt))
        .ToListAsync(cancellationToken);
    return Results.Ok(orders);
})
.WithName("ListOrders")
.WithTags("Orders")
.Produces<IReadOnlyList<OrderResponse>>();

app.MapPost("/api/orders", async (
    CreateOrderRequest request,
    TelemetryBridgeDbContext database,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.Channel is not ("web" or "mobile" or "partner"))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Channel)] = ["Channel must be web, mobile, or partner."]
        });
    }

    using var operation = TelemetryOperation.Start("order.create", "create");
    var inventory = httpClientFactory.CreateClient("inventory");
    using var reservationResponse = await inventory.PostAsJsonAsync(
        "/api/inventory/reservations",
        new { request.Channel },
        cancellationToken);
    if (!reservationResponse.IsSuccessStatusCode)
    {
        operation.RecordException(new InvalidOperationException("Inventory reservation failed."));
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The order dependency is temporarily unavailable.");
    }

    var order = new Order
    {
        Id = Guid.NewGuid(),
        Channel = request.Channel,
        CreatedAt = DateTimeOffset.UtcNow
    };

    var messageContext = TelemetryMessageContext.Capture();
    database.Orders.Add(order);
    database.WorkItems.Add(new WorkItem
    {
        Id = Guid.NewGuid(),
        Operation = "order.created",
        CreatedAt = DateTimeOffset.UtcNow,
        TraceParent = messageContext.TraceParent,
        TraceState = messageContext.TraceState,
        Baggage = messageContext.Baggage
    });
    await database.SaveChangesAsync(cancellationToken);
    SampleApiLogs.OrderCreated(logger, order.Channel);

    return Results.Created($"/api/orders/{order.Id}", new OrderResponse(order.Id, order.Channel, order.CreatedAt));
})
.WithName("CreateOrder")
.WithTags("Orders")
.Produces<OrderResponse>(StatusCodes.Status201Created)
.ProducesValidationProblem();

app.MapGet("/api/orders/{id:guid}", async (
    Guid id,
    TelemetryBridgeDbContext database,
    CancellationToken cancellationToken) =>
{
    using var operation = TelemetryOperation.Start("order.get", "get");
    var order = await database.Orders
        .AsNoTracking()
        .Where(order => order.Id == id)
        .Select(order => new OrderResponse(order.Id, order.Channel, order.CreatedAt))
        .SingleOrDefaultAsync(cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
})
.WithName("GetOrder")
.WithTags("Orders")
.Produces<OrderResponse>()
.Produces(StatusCodes.Status404NotFound);

app.MapMethods("/api/payments/{**path}", ["GET", "POST"], (HttpContext context) =>
{
    using var operation = TelemetryOperation.Start("payment.modern", "process");
    operation?.SetTag("telemetrybridge.modernization.implementation", "modern");
    return Results.Ok(new
    {
        implementation = "modern",
        path = context.Request.Path.Value,
        method = context.Request.Method
    });
})
.WithName("ModernPayments")
.WithTags("Payments");

app.MapHealthChecks("/health");

await InitializeDatabaseAsync(app.Services, app.Logger);
await app.RunAsync();

static async Task InitializeDatabaseAsync(IServiceProvider services, ILogger logger)
{
    await using var scope = services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<TelemetryBridgeDbContext>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await DatabaseInitializer.EnsureCreatedAsync(database);
            return;
        }
        catch (Exception exception) when (attempt < 10)
        {
            SampleApiLogs.DatabaseInitializationRetry(logger, attempt, exception);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

internal sealed record CreateOrderRequest(string Channel);
internal sealed record OrderResponse(Guid Id, string Channel, DateTimeOffset CreatedAt);

/// <summary>Entry point exposed for integration testing.</summary>
public partial class Program;
