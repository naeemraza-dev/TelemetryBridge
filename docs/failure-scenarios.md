# Failure scenarios

| Failure | Expected behavior | Validation |
|---|---|---|
| Collector unavailable | SDK batches/drops asynchronously; business request continues | Stop Collector, create an order, confirm 201 and exporter diagnostics |
| Datadog/Azure/Tempo/Loki unavailable | Collector retries and queues; applications remain healthy | Stop backend or use invalid test endpoint; observe queue/failure alerts |
| Queue full / tail capacity | Collector refuses/drops by policy and alerts; no backpressure into business calls | Load test until queue/refusal threshold |
| Slow database | Request latency/error trace and database span; cancellation respected | Inject PostgreSQL delay in isolated testing |
| Internal API failure | Modern API returns bounded 503; span is error | Stop internal API and POST an order |
| Malformed trace context | Instrumentation starts a valid new trace; no crash | Send malformed `traceparent` |
| Untrusted baggage | Automatic baggage propagation disabled; message baggage deny-by-default | Unit test plus request with synthetic baggage |
| Oversized/sensitive payload | Bodies are not captured; Collector deletes dangerous attributes | Redaction tests and Tempo/Loki inspection |
| Legacy/modern unhealthy | Health monitor selects healthy fallback and records event | Stop one backend and call its routed operation |
| Bad migration | Admin ETag prevents lost update; Admin rollback restores history | Integration tests and staged rollback drill |

Telemetry is never awaited as part of a business export call. OTLP exporters and NLog targets
batch asynchronously. Persistent queues improve recovery but are not a reason to let telemetry
consume unbounded disk or memory; alert, cap, and test exhaustion.
