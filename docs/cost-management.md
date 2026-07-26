# Telemetry cost management

Cost is governed before vendor pricing: traffic, spans per trace, bytes per span/log, sampling,
metric-series cardinality, retention, indexing, backend fan-out, and Collector capacity.

Run the vendor-neutral estimator:

```powershell
dotnet run --project src/TelemetryBridge.CostEstimator -- `
  src/TelemetryBridge.CostEstimator/example-input.json
```

The JSON input accepts requests/second, spans/trace, average span bytes, trace sample percent,
logs/request, average log bytes, metric-series count, retention days, exporter count, and
optional externally supplied prices. Output includes spans/day, trace and log GB/day, export
GB/day, retained monthly volume, optional vendor ingestion cost, and optional total including
Collector infrastructure. Set `pricePerIngestedGb` and
`monthlyCollectorInfrastructureCost` from the current contract. Prices are never embedded in
code because contracts and vendor rates change.

## Control loop

1. Measure accepted and exported records plus real payload size.
2. Normalize routes and enforce attribute allowlists before creating dashboards.
3. Select head sampling from traffic and Collector capacity.
4. Use tail sampling for errors/outliers within the received population.
5. Alert on series growth, queue pressure, refusal, and exporter failure.
6. Recalculate when traffic, retention, schema, sampling, or backend count changes.

Metric-series count can dominate cost even when request volume is stable. Never use user,
tenant, order, request, trace, raw URL, query, or exception message as a metric dimension.
Export fan-out is explicit multiplication, not redundancy for free.
