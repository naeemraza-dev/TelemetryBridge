using TelemetryBridge.Core;

namespace TelemetryBridge.UnitTests;

public sealed class CostEstimatorTests
{
    [Fact]
    public void EstimateAccountsForSamplingAndBackendDuplication()
    {
        var result = TelemetryCostEstimator.Estimate(new(
            RequestsPerSecond: 100,
            AverageSpansPerTrace: 10,
            AverageSpanSizeBytes: 1000,
            TraceSamplingPercentage: 10,
            LogsPerRequest: 1,
            AverageLogSizeBytes: 500,
            MetricSeriesCount: 1000,
            RetentionDays: 7,
            NumberOfExportBackends: 2,
            PricePerIngestedGb: 0.25,
            MonthlyCollectorInfrastructureCost: 50));

        Assert.Equal(8_640_000, result.EstimatedSpansPerDay);
        Assert.Equal(
            2 * (result.EstimatedTraceVolumeGbPerDay + result.EstimatedLogVolumeGbPerDay),
            result.EstimatedExportVolumeGbPerDay);
        Assert.Equal(
            result.EstimatedMonthlyVendorIngestionCost + 50,
            result.EstimatedMonthlyTotalCost);
    }
}
