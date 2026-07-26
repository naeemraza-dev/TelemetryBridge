internal static partial class SampleApiLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Created an order through channel {OrderChannel}")]
    public static partial void OrderCreated(ILogger logger, string orderChannel);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "PostgreSQL is not ready; database initialization attempt {Attempt} failed")]
    public static partial void DatabaseInitializationRetry(ILogger logger, int attempt, Exception exception);
}
