# Security and privacy

Treat telemetry as potentially sensitive production data. The default design excludes request
and response bodies, authorization headers, cookies, token values, SQL text/parameters, and
user-identifying fields.

## Threats and controls

| Threat | Controls |
|---|---|
| Secret/PII leakage through tags | SDK allowlists, bounded values, Collector deletion, regression tests |
| Telemetry exfiltration | TLS, authenticated OTLP, egress allowlists, regional endpoints |
| Unauthenticated/spoofed ingestion | mTLS or gateway authentication, network policy, service identity |
| Collector compromise | Minimal image, non-root runtime, patched versions, secret isolation, restricted diagnostics |
| Dashboard overexposure | SSO, RBAC, least privilege, audit logs, short retention |
| High-cardinality denial of service | Stable names, route normalization, limits, memory limiter, backend quotas |
| Cross-tenant leakage | No tenant field by default; separately authorized data partitions if required |
| Backend duplication | Single exporter ownership and migration-time ingestion inventory |

The local stack deliberately uses HTTP, open receivers, local passwords, and a debug exporter.
It must not be exposed publicly or copied unchanged to production.

Production deployments need secret-store injection, encrypted transport, receiver authentication,
private health/debug endpoints, Kubernetes network policy, dashboard SSO/RBAC, audit logging,
retention policy, data-residency approval, queue encryption where applicable, and incident
response procedures.

