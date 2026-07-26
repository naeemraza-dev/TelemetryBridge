using TelemetryBridge.Core;

namespace TelemetryBridge.UnitTests;

public sealed class MigrationConfigurationStoreTests
{
    [Fact]
    public async Task UpdateUsesOptimisticConcurrencyAndCreatesHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "telemetrybridge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var store = new MigrationConfigurationStore(Path.Combine(root, "migration.json"));
            var initial = await store.ReadAsync();
            var updated = await store.UpdateAsync(initial.Version, "rollout", 25, true, "test");
            var conflict = await store.UpdateAsync(initial.Version, "modern", 100, false, "test");
            var history = await store.HistoryAsync();

            Assert.NotNull(updated);
            Assert.Equal(25, updated.PaymentModernPercentage);
            Assert.Null(conflict);
            Assert.Contains(history, item => item.Version == initial.Version);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
