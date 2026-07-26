# Load and overhead tests

Run the stack, then execute k6 without installing it locally:

```powershell
docker run --rm -i grafana/k6:0.57.0 run - `
  -e BASE_URL=http://host.docker.internal:8080 `
  -e REQUESTS_PER_SECOND=20 `
  -e DURATION=2m `
  - < load/k6/orders.js
```

Capture one baseline with `TelemetryBridge__Enabled=false` on every application service and
one run with telemetry enabled. Keep image versions, database state, request rate, duration,
CPU allocation, and warm-up identical. Record application p50/p95/p99, request failures,
Collector accepted/refused/export-failed counts, process CPU/RSS, exporter queue occupancy,
and retained-to-accepted span ratio.

For tail-sampling capacity, start
`docker compose -f docker-compose.yml -f docker-compose.tail-sampling.yml up --build`, increase
`REQUESTS_PER_SECOND` stepwise, and stop when the p95 SLO, refused-span, or memory threshold is
crossed. Tail-sampling results are hardware-specific; do not publish unmeasured numbers.
