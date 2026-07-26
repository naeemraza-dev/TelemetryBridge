# Operations runbook

## Health and evidence

- Facade: `http://localhost:8080/health`
- Modern, internal, legacy, admin: ports `8081`, `8082`, `8083`, `8084`, path `/health`
- Collector: `http://localhost:13133/`
- Tempo: `http://localhost:3200/ready`
- Prometheus targets/rules: `http://localhost:9090/targets`, `/rules`
- Grafana: `http://localhost:3000`

Monitor accepted/refused records, export failures, queue size/capacity, retry, memory, CPU,
restarts, sampling effectiveness, missing application data, and backend throttling.

## Triage

1. Confirm the business endpoint is healthy; telemetry export is asynchronous.
2. Check facade target/fallback events and backend health.
3. Check Collector logs, health, refusal, exporter failures, and queue occupancy.
4. Check Prometheus targets and Tempo/Loki readiness and storage.
5. Confirm service identity, sampling, endpoint, TLS, authentication, CORS, and secrets.
6. If capacity is exhausted, reduce a non-critical signal/head rate through a controlled
   rollout; never enable verbose payload export in production.
7. After recovery, verify queues drain and refusal/failure alerts clear.

## Strangler rollback

Read migration configuration and its ETag. An Admin restores a historical version with
`POST /api/configuration/rollback/{version}` and the current `If-Match`. The restore is a new,
audited version. Do not overwrite a `409` conflict. See `strangler-migration.md`.

## Collector/backend outage

Applications continue because OTLP batching/export is not in the business response path.
Collectors retry with bounded queues. A full queue refuses/drops and alerts rather than
allowing unbounded memory. Do not repeatedly restart a pressured Collector; establish backend
recovery and capacity first. Production persistent queue storage must have bounded, monitored,
encrypted-at-rest storage appropriate to the signal.

## Upgrade

Review release/semantic-convention changes, pin versions, build/test/package, validate every
Collector file with the exact image, parse all Compose overlays, start the stack, run the live
E2E test, inspect one trace/log/metric journey, exercise fallback/rollback, then promote the
same artifacts. Keep the previous container/package/config version available for rollback.
