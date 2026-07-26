# Sampling strategy

TelemetryBridge supports `AlwaysOn`, `AlwaysOff`, `TraceIdRatio`, `ParentBasedAlwaysOn`, and
`ParentBasedTraceIdRatio` at the application SDK.

| Environment example | Example head rate |
|---|---:|
| Development / test | 100% |
| Staging | 25% |
| Production | 5-10% |

These are starting points, not universal rules. Parent-based sampling respects the upstream
decision and immediately bounds application/network/Collector work. It cannot know the final
outcome, so it may discard rare errors or slow traces.

The implemented tail tier consistently hashes traces across decision collectors, drops health
noise, retains errors, traces above two seconds, critical normalized routes, configured
incident traces, and 5% of other received traces. Tail sampling provides better retained value
but buffers spans in memory, delays export, requires trace affinity, and can produce incomplete
traces when spans are late or the tier is resharded.

```powershell
docker compose -f docker-compose.yml -f docker-compose.tail-sampling.yml up --build
```

Tail sampling cannot recover spans removed by head sampling. Size the hybrid head rate so the
tail tier sees enough of the population for its error/outlier policies to be useful.

| Scenario | Recommended approach |
|---|---|
| Low traffic, critical service | High or 100% parent-based head sampling |
| High traffic, strict predictable volume | Ratio-based head sampling |
| Retain errors and latency outliers | Tail sampling over a sufficient received population |
| Large distributed environment | Trace-ID-balanced two-tier Collectors |
| Incident investigation | Time-limited reviewed sampling increase |
| Sensitive or regulated workload | Redaction/filtering before every exporter |

Watch decision-buffer memory, accepted/refused/exported spans, queue pressure, late spans, and
backend throttle. Change one dimension at a time and record the cost-estimator result.
