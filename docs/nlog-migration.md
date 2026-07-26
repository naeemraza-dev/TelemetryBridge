# NLog migration

`TelemetryBridge.NLog` lets an existing application keep its current NLog targets while adding
trace correlation and, where required, direct OTLP log export.

## ASP.NET Core setup

Reference `TelemetryBridge.NLog`, retain the existing `NLog.config`, and add:

```csharp
builder.Logging.AddTelemetryBridgeNLog();
builder.Services.AddTelemetryBridge(builder.Configuration);

var app = builder.Build();
app.UseTelemetryBridge();
app.UseTelemetryBridgeNLogCorrelation();
```

The correlation middleware publishes `TraceId`, `SpanId`, request/correlation IDs,
`service.name`, and `deployment.environment.name` to NLog scopes. For code that calls NLog
directly, load the package extension and use the safe renderer:

```xml
<extensions>
  <add assembly="TelemetryBridge.NLog" />
</extensions>
<target xsi:type="Console" name="existing"
  layout="${longdate}|${level}|trace=${telemetrybridge-correlation:item=TraceId}|span=${telemetrybridge-correlation:item=SpanId}|${message} ${exception:format=tostring}" />
```

Supported renderer items are `TraceId`, `SpanId`, `TraceFlags`, `ServiceName`, and
`Environment`. The renderer emits no value outside an active trace and never invents a trace.

## Single-export rule

Choose one log export owner for each logger category:

- Preferred: application code uses `ILogger`; the OpenTelemetry .NET logging provider exports
  OTLP, while NLog retains file/console/legacy targets.
- Transitional: direct NLog categories use `NLog.Targets.OpenTelemetryProtocol`; exclude those
  categories from another OTLP logging provider.

Never attach both an NLog OTLP target and the OpenTelemetry `ILogger` exporter to the same
record stream. The sample legacy API demonstrates direct NLog OTLP while preserving console.
Test record counts before rollout.

Direct NLog calls do not automatically gain `ILogger` scopes in non-HTTP/background code.
Prefer structured `ILogger` source-generated messages for new code, then migrate legacy
categories gradually. Preserve exception objects, not interpolated exception text, and never
log request bodies, tokens, cookies, connection strings, or database parameters.

## Rollout and rollback

Deploy correlation-only first, confirm existing files/sinks are unchanged, then enable OTLP
for a small category. Compare counts and trace IDs in staging. Roll back by removing the OTLP
target/rule while leaving the existing targets and correlation renderer intact.
