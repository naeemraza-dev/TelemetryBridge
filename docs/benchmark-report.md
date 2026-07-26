# Benchmark report

This file is the reproducible report template. Populate it from `load/k6/orders.js` on the
target deployment hardware; no synthetic numbers are claimed as measured results.

| Field | Telemetry disabled | Head sampled | Two-tier tail sampled |
|---|---:|---:|---:|
| Date / commit | | | |
| Docker CPUs / memory | | | |
| Requests per second | | | |
| p50 / p95 / p99 latency | | | |
| Request failure rate | | | |
| Application CPU / RSS | | | |
| Accepted / refused spans per second | | | |
| Export failures | | | |
| Queue peak / capacity | | | |
| Collector CPU / RSS | | | |
| Retained / accepted span ratio | | | |

Calculate overhead as `(enabled - disabled) / disabled × 100` using at least three runs after
warm-up. Report the median and range. Include any backend throttling, dropped load-generator
iterations, database saturation, and sampling configuration so the result can be reproduced.
