using Microsoft.EntityFrameworkCore;

namespace TelemetryBridge.Persistence;

/// <summary>Initializes the local demonstration schema without embedding credentials.</summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the sample schema. Production deployments must replace this convenience method
    /// with reviewed, versioned EF Core migrations.
    /// </summary>
    public static async Task EnsureCreatedAsync(
        TelemetryBridgeDbContext database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await database.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        // EnsureCreated does not add newly introduced tables to an existing local database.
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS work_items (
                "Id" uuid NOT NULL,
                "Operation" character varying(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "TraceParent" character varying(128) NULL,
                "TraceState" character varying(512) NULL,
                "Baggage" character varying(512) NULL,
                CONSTRAINT "PK_work_items" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_work_items_ProcessedAt_CreatedAt"
                ON work_items ("ProcessedAt", "CreatedAt");
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
