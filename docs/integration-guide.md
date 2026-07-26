# ASP.NET Core integration guide

## Minimal integration

Add a reference to `TelemetryBridge.AspNetCore`, configure service identity, and register:

```csharp
builder.Services.AddTelemetryBridge(
    builder.Configuration,
    options =>
    {
        options.ServiceName = "orders-api";
        options.ServiceVersion = "1.0.0";
        options.Environment = builder.Environment.EnvironmentName;
    });

var app = builder.Build();
app.UseTelemetryBridge();
```

Place the middleware early enough to wrap application endpoints. It creates a logging scope
containing `TraceId`, `SpanId`, request ID, bounded correlation ID, service name, and environment.
The correlation ID helps external support workflows but does not replace W3C trace context.

## Configuration

```json
{
  "TelemetryBridge": {
    "Enabled": true,
    "ServiceName": "orders-api",
    "ServiceNamespace": "commerce",
    "ServiceVersion": "1.0.0",
    "Environment": "production",
    "Otlp": {
      "Endpoint": "https://collector.internal.example:4317",
      "Protocol": "Grpc"
    },
    "Tracing": {
      "Enabled": true,
      "SamplingMode": "ParentBasedTraceIdRatio",
      "SamplingRatio": 0.1
    },
    "Metrics": { "Enabled": true },
    "Logging": {
      "Enabled": true,
      "IncludeFormattedMessage": true,
      "IncludeScopes": true
    }
  }
}
```

Environment variables override these values. Secrets must come from the deployment secret
store, not JSON. Use TLS and authenticated Collector endpoints outside local development.

## Custom instrumentation

Use controlled names and values:

```csharp
using var operation = TelemetryOperation.Start("order.process", "create");
try
{
    await ProcessAsync(cancellationToken);
}
catch (Exception exception)
{
    operation.RecordException(exception);
    throw;
}
```

Never use order IDs, user IDs, emails, raw URLs, SQL, exception messages, or arbitrary text as
metric attributes. Add custom attributes only through reviewed, low-cardinality conventions.

## Existing instrumentation

- Keep only one ASP.NET Core, HttpClient, and database instrumentation source. Disable the
  equivalent proprietary agent feature before enabling duplicate SDK instrumentation.
- If legacy Application Insights auto-collection is active, inventory its modules and ingestion
  path first. Do not export the same signal through both paths.
- Existing logging providers remain active. The OTel logging provider is additive; check for an
  existing OTLP log exporter to prevent duplicate records.
- Validate trace continuity, volume, and redaction in non-production before increasing rollout.

## SQL Server compatibility

The reusable telemetry packages do not depend on the PostgreSQL sample. An existing EF Core
SQL Server application keeps `UseSqlServer` and its normal `Microsoft.Data.SqlClient`
configuration. EF Core command tracing is registered by `TelemetryBridge.AspNetCore`; add the
OpenTelemetry SqlClient instrumentation package only if direct ADO.NET operations also require
spans, and ensure another APM agent is not already instrumenting SqlClient.

Keep query-parameter capture disabled. `CaptureParameterizedTextInDevelopment` is rejected
outside Development, and the Collector deletes `db.query.text`/`db.statement` before export in
the supplied configurations. Validate the precise database semantic attributes after every
instrumentation upgrade because those conventions are still evolving.

## Rollback

Set `TelemetryBridge:Enabled=false` or an equivalent environment override and restart the
workload. This disables TelemetryBridge providers without changing business code. Preserve
the Collector route during a staged rollback until buffered telemetry is drained.

## Production checklist

- Unique stable service name/namespace/version and deployment environment
- Authenticated TLS OTLP endpoint
- Parent-based sampler chosen from measured traffic and incident requirements
- Collector retry, memory, queue, and drop alerts
- Sensitive-attribute regression tests
- No duplicate APM/log exporters
- Dashboard access controlled through SSO/RBAC
- Backend retention and regional routing approved
- Rollback tested in staging
