using System.Text.Json;

namespace TelemetryBridge.Core;

/// <summary>Versioned operational configuration for Strangler routing.</summary>
public sealed record MigrationConfiguration(
    long Version,
    string Mode,
    int PaymentModernPercentage,
    bool HeaderRoutingEnabled,
    DateTimeOffset UpdatedAt,
    string UpdatedBy)
{
    /// <summary>Creates the secure observe-only default.</summary>
    public static MigrationConfiguration Default { get; } =
        new(1, "observe", 0, false, DateTimeOffset.UtcNow, "system");
}

/// <summary>Atomic file-backed configuration suitable for the containerized reference implementation.</summary>
public sealed class MigrationConfigurationStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath;
    private readonly string historyPath;

    /// <summary>Creates a store rooted at the supplied file path.</summary>
    public MigrationConfigurationStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = Path.GetFullPath(filePath);
        historyPath = Path.Combine(Path.GetDirectoryName(this.filePath)!, "history");
    }

    /// <summary>Reads the active configuration, creating an observe-only default when absent.</summary>
    public async Task<MigrationConfiguration> ReadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Updates configuration when the expected version matches; otherwise returns null.</summary>
    public async Task<MigrationConfiguration?> UpdateAsync(
        long expectedVersion,
        string mode,
        int paymentModernPercentage,
        bool headerRoutingEnabled,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        Validate(mode, paymentModernPercentage);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
            if (current.Version != expectedVersion)
            {
                return null;
            }

            await ArchiveAsync(current, cancellationToken).ConfigureAwait(false);
            var updated = new MigrationConfiguration(
                current.Version + 1,
                mode.ToLowerInvariant(),
                paymentModernPercentage,
                headerRoutingEnabled,
                DateTimeOffset.UtcNow,
                updatedBy);
            await WriteAtomicAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Returns configuration history ordered from newest to oldest.</summary>
    public async Task<IReadOnlyList<MigrationConfiguration>> HistoryAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(historyPath))
        {
            return [];
        }

        var result = new List<MigrationConfiguration>();
        foreach (var path in Directory.EnumerateFiles(historyPath, "*.json").OrderByDescending(path => path))
        {
            await using var stream = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<MigrationConfiguration>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    /// <summary>Restores a historical version as a new version when the active ETag still matches.</summary>
    public async Task<MigrationConfiguration?> RollbackAsync(
        long expectedVersion,
        long restoreVersion,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
            if (current.Version != expectedVersion)
            {
                return null;
            }

            var path = Path.Combine(historyPath, $"{restoreVersion:D10}.json");
            if (!File.Exists(path))
            {
                throw new KeyNotFoundException($"Migration configuration version {restoreVersion} was not found.");
            }

            await using var stream = File.OpenRead(path);
            var historical = await JsonSerializer.DeserializeAsync<MigrationConfiguration>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Historical migration configuration is invalid.");

            await ArchiveAsync(current, cancellationToken).ConfigureAwait(false);
            var restored = historical with
            {
                Version = current.Version + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = updatedBy
            };
            await WriteAtomicAsync(restored, cancellationToken).ConfigureAwait(false);
            return restored;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => gate.Dispose();

    private async Task<MigrationConfiguration> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await WriteAtomicAsync(MigrationConfiguration.Default, cancellationToken).ConfigureAwait(false);
            return MigrationConfiguration.Default;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<MigrationConfiguration>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Migration configuration is invalid.");
    }

    private async Task ArchiveAsync(MigrationConfiguration configuration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(historyPath);
        var path = Path.Combine(historyPath, $"{configuration.Version:D10}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task WriteAtomicAsync(MigrationConfiguration configuration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        File.Move(temporary, filePath, true);
    }

    private static void Validate(string mode, int percentage)
    {
        if (mode is not ("observe" or "shadow" or "rollout" or "modern"))
        {
            throw new ArgumentException("Mode must be observe, shadow, rollout, or modern.", nameof(mode));
        }
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }
    }
}
