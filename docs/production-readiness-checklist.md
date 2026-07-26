# Production-readiness checklist

Kubernetes/Helm is intentionally deferred for this delivery; the container controls below
still apply to Docker Desktop and any later orchestrator.

- [ ] Images are pinned, vulnerability-scanned, signed, and run as non-root/read-only where possible.
- [ ] OTLP uses authenticated TLS/mTLS on private networks; debug, pprof, zPages, Grafana, and health ports are not public.
- [ ] API/admin keys and vendor credentials come from a secret store and rotate successfully.
- [ ] Stable service identity/version/environment and regional routing are approved.
- [ ] One instrumentation/export owner exists for each signal; no duplicate APM or logs.
- [ ] Sensitive-data and cardinality tests pass against representative traffic.
- [ ] Head/tail sampling and Collector memory are sized from a measured benchmark.
- [ ] Export queues, refusal, drops, retry, restarts, backend throttling, and missing telemetry alert.
- [ ] Dashboard SSO/RBAC, audit, retention, residency, deletion, and incident access are approved.
- [ ] Database migrations replace local `EnsureCreated`; backups and restore are tested.
- [ ] Strangler contract tests, rollout gates, health fallback, and admin rollback are exercised.
- [ ] Collector/backend outage does not fail business requests; recovery drains queues within SLO.
- [ ] On-call owns the runbook, alert routes, vendor quotas, and rollback authority.
- [ ] Package artifacts are produced from a tagged build and tested in a representative existing service.
