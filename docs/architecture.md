# Architecture and decisions

## Runtime architecture

```mermaid
flowchart LR
  UI[React + browser package] -->|W3C trace context| F[YARP Strangler facade]
  F -->|orders / v2 / rollout| M[Modern API]
  F -->|customers / baseline| L[Legacy API + NLog]
  M -->|HttpClient| I[Internal inventory API]
  M -->|EF Core + Npgsql| DB[(PostgreSQL)]
  W[Background worker] -->|poll durable work| DB
  UI -->|OTLP/HTTP| C[Collector gateway]
  F -->|OTLP/gRPC| C
  M -->|OTLP/gRPC| C
  L -->|OTLP logs + signals| C
  I -->|OTLP/gRPC| C
  W -->|OTLP/gRPC| C
  C --> T[Tempo]
  C --> P[Prometheus]
  C --> K[Loki]
  C -. optional .-> D[Datadog]
  C -. optional .-> A[Azure Monitor]
  T --> G[Grafana]
  P --> G
  K --> G
```

Business code and reusable packages contain no Datadog, Azure, Grafana, or other proprietary
SDK calls. The Collector owns routing, redaction, retries, queues, and vendor credentials.

## Connected request and worker flow

```mermaid
sequenceDiagram
  participant B as Browser
  participant F as Facade
  participant M as Modern API
  participant I as Internal API
  participant P as PostgreSQL
  participant W as Worker
  B->>F: POST /api/orders + traceparent
  F->>M: proxied request + traceparent
  M->>I: reserve inventory
  I-->>M: reservation
  M->>P: insert order + durable work context
  P-->>M: committed
  M-->>F: 201
  F-->>B: 201 + correlation ID
  W->>P: claim work
  W->>W: consumer span with stored parent
  W->>P: mark processed
```

The durable envelope stores `traceparent`, `tracestate`, and only explicitly allowlisted
baggage. The worker restores the parent for a single-message consumer. `CreateLinks` supports
batch/fan-in work where no single parent is correct.

## Scalable tail sampling

```mermaid
flowchart LR
  Apps[Applications] --> A[First-tier Collector]
  A -->|trace-ID consistent hash| S1[Tail sampler 1]
  A -->|trace-ID consistent hash| S2[Tail sampler 2]
  S1 --> Backends[Trace backend]
  S2 --> Backends
  A -->|metrics and logs| G[Gateway Collector]
```

All spans for one trace reach one decision node. Scaling the decision tier changes hash
ownership and can temporarily produce incomplete traces, so capacity changes require a
controlled rollout and a decision-wait drain.

## NLog bridge

```mermaid
flowchart LR
  Direct[Direct NLog calls] --> Scope[Correlation middleware / custom renderer]
  MEL[ILogger calls] --> NLog[NLog provider]
  Scope --> Existing[Existing file/console targets]
  Scope --> OTLP[NLog OTLP target for selected legacy categories]
  MEL --> OTel[OpenTelemetry ILogger provider]
  OTLP --> Collector
  OTel --> Collector
```

Each log category has exactly one OTLP owner. Existing non-OTLP targets remain intact.

## Strangler control flow

```mermaid
flowchart TD
  Request --> Contract[Stable public OpenAPI]
  Contract --> Rule{Path / method / API version / mode}
  Rule --> Legacy
  Rule --> Modern
  Rule -->|safe GET in shadow| Shadow[Bounded async modern validation]
  Legacy --> Health{Backend healthy?}
  Modern --> Health
  Health -->|no| Fallback[Healthy alternate + telemetry event]
  Health -->|yes| Response
  Fallback --> Response
  Admin[Authenticated admin API] -->|ETag + audit + history| Config[Versioned migration config]
  Config --> Rule
```

## Failure and retry flow

```mermaid
flowchart LR
  App -->|non-blocking batch| Collector
  Collector --> Memory[Memory limiter]
  Memory --> Queue[Bounded sending queue]
  Queue --> Backend
  Backend -->|temporary failure| Retry[Backoff/retry]
  Retry --> Queue
  Queue -->|capacity exceeded| Drop[Refusal/drop metrics + alert]
  Drop -. never fails .-> Business[Business operation]
```

## Key decisions

| Decision | Benefit | Trade-off |
|---|---|---|
| OTLP plus Collector boundary | Vendor-neutral workloads and centralized governance | Collector is production infrastructure |
| Secure allowlist plus Collector deletion | Defense in depth against sensitive/high-cardinality data | Schema changes require review |
| Parent-based head sampling | Immediate predictable volume and upstream consistency | Cannot retain an error already dropped |
| Trace-ID-balanced tail tier | Outcome-aware retention at scale | Stateful memory and operational complexity |
| Durable DB work envelope | Runnable without a broker while proving async propagation | Production high-throughput systems should use an outbox/broker |
| File migration store for Docker | Atomic, inspectable reference implementation | Single-host only; production needs HA configuration |
| Separate Core/AspNetCore/NLog/browser packages | Incremental adoption | Coordinated versioning is required |
| Health-based Strangler fallback | Rollback without client changes | Health alone is not a full compatibility guarantee |

## Repository boundaries

- `TelemetryBridge.Core`: vendor-neutral activities, metrics, policy, propagation, cost model,
  and migration state.
- `TelemetryBridge.AspNetCore`: one-call OpenTelemetry setup, validation, safe database
  defaults, and correlation middleware.
- `TelemetryBridge.NLog`: direct/`ILogger` NLog compatibility and correlation.
- `TelemetryBridge.Browser`: sanitized browser tracing and approved-origin propagation.
- Facade/admin/sample projects: runnable proof and operational workflow, not package internals.
- `collector`, `deployment`, `dashboards`, `load`, and `docs`: infrastructure and operations.

Kubernetes and Helm are intentionally deferred. Container images, Compose, health checks,
non-root .NET runtimes, vendor overlays, tests, and production guidance are included.
