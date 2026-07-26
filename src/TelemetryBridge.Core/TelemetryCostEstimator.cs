namespace TelemetryBridge.Core;

/// <summary>Vendor-neutral telemetry volume inputs.</summary>
public sealed record TelemetryCostInputs(
    double RequestsPerSecond,
    double AverageSpansPerTrace,
    double AverageSpanSizeBytes,
    double TraceSamplingPercentage,
    double LogsPerRequest,
    double AverageLogSizeBytes,
    long MetricSeriesCount,
    int RetentionDays,
    int NumberOfExportBackends,
    double? PricePerIngestedGb = null,
    double MonthlyCollectorInfrastructureCost = 0);

/// <summary>Estimated volumes without hard-coded vendor prices.</summary>
public sealed record TelemetryCostEstimate(
    double EstimatedSpansPerDay,
    double EstimatedTraceVolumeGbPerDay,
    double EstimatedLogVolumeGbPerDay,
    double EstimatedExportVolumeGbPerDay,
    double EstimatedMonthlyExportVolumeGb,
    double EstimatedRetainedVolumeGb,
    long MetricSeriesCount,
    double? EstimatedMonthlyVendorIngestionCost,
    double? EstimatedMonthlyTotalCost);

/// <summary>Calculates approximate telemetry volumes for capacity and cost planning.</summary>
public static class TelemetryCostEstimator
{
    private const double SecondsPerDay = 86_400;
    private const double BytesPerGigabyte = 1_000_000_000;

    /// <summary>Estimates volumes and validates that all inputs are within meaningful bounds.</summary>
    public static TelemetryCostEstimate Estimate(TelemetryCostInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.RequestsPerSecond < 0
            || inputs.AverageSpansPerTrace < 0
            || inputs.AverageSpanSizeBytes < 0
            || inputs.TraceSamplingPercentage is < 0 or > 100
            || inputs.LogsPerRequest < 0
            || inputs.AverageLogSizeBytes < 0
            || inputs.MetricSeriesCount < 0
            || inputs.RetentionDays < 0
            || inputs.NumberOfExportBackends < 1
            || inputs.PricePerIngestedGb < 0
            || inputs.MonthlyCollectorInfrastructureCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputs), "Cost inputs contain invalid values.");
        }

        var requestsPerDay = inputs.RequestsPerSecond * SecondsPerDay;
        var spansPerDay = requestsPerDay
            * inputs.AverageSpansPerTrace
            * (inputs.TraceSamplingPercentage / 100);
        var traceGb = spansPerDay * inputs.AverageSpanSizeBytes / BytesPerGigabyte;
        var logGb = requestsPerDay * inputs.LogsPerRequest * inputs.AverageLogSizeBytes / BytesPerGigabyte;
        var signalGb = traceGb + logGb;
        var exportedGb = signalGb * inputs.NumberOfExportBackends;
        var monthlyExportGb = exportedGb * 30;
        double? vendorCost = inputs.PricePerIngestedGb is null
            ? null
            : monthlyExportGb * inputs.PricePerIngestedGb.Value;
        return new(
            spansPerDay,
            traceGb,
            logGb,
            exportedGb,
            monthlyExportGb,
            signalGb * inputs.RetentionDays,
            inputs.MetricSeriesCount,
            vendorCost,
            vendorCost + inputs.MonthlyCollectorInfrastructureCost);
    }
}
