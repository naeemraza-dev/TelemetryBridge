using Microsoft.EntityFrameworkCore;

namespace TelemetryBridge.Persistence;

/// <summary>Sample PostgreSQL database context.</summary>
public sealed class TelemetryBridgeDbContext(DbContextOptions<TelemetryBridgeDbContext> options)
    : DbContext(options)
{
    /// <summary>Gets the order set.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Gets the durable work queue used by the sample worker.</summary>
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(order => order.Id);
            entity.Property(order => order.Channel).HasMaxLength(32).IsRequired();
            entity.HasIndex(order => order.CreatedAt);
        });

        modelBuilder.Entity<WorkItem>(entity =>
        {
            entity.ToTable("work_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Operation).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TraceParent).HasMaxLength(128);
            entity.Property(item => item.TraceState).HasMaxLength(512);
            entity.Property(item => item.Baggage).HasMaxLength(512);
            entity.HasIndex(item => new { item.ProcessedAt, item.CreatedAt });
        });
    }
}
