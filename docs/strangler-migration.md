# Strangler migration

The facade is the stable public endpoint. `/api/orders/**` routes modern,
`/api/customers/**` routes legacy, and `/api/payments/**` follows the versioned migration
configuration stored on the shared `migration-config` Docker volume.

| Mode | Payments behavior |
|---|---|
| `observe` | Legacy only; collect the baseline |
| `shadow` | Serve legacy; enqueue bounded, read-only GET validation against modern |
| `rollout` | Deterministically route the configured trace-ID percentage to modern |
| `modern` | Modern primary, with health-based fallback |

Shadowing is intentionally limited to GET. POST/other state-changing calls are never duplicated.
The bounded shadow queue drops validation work instead of delaying the user response. The
facade records the controlled target, decision duration, fallback events, and shadow outcomes.

`X-TelemetryBridge-Route: modern|legacy` works only when header routing is enabled and the
facade runs in Development. `X-Api-Version: 1|2` provides explicit legacy/modern selection for
payments. Public production clients should use the published contract rather than test-routing
headers.

## Change a rollout

Set strong keys before starting Compose:

```powershell
$env:TELEMETRYBRIDGE_ADMIN_KEY="<secret>"
$env:TELEMETRYBRIDGE_OPERATOR_KEY="<different-secret>"
docker compose up --build
```

Read the active ETag:

```powershell
curl.exe -H "X-TelemetryBridge-Admin-Key: <operator-secret>" `
  http://localhost:8084/api/configuration/migration
```

Update using the returned numeric ETag:

```powershell
curl.exe -X PUT `
  -H "X-TelemetryBridge-Admin-Key: <operator-secret>" `
  -H 'If-Match: "1"' -H "Content-Type: application/json" `
  -d '{"mode":"rollout","paymentModernPercentage":10,"headerRoutingEnabled":false}' `
  http://localhost:8084/api/configuration/migration
```

Only the Admin role can roll back. A rollback restores historical settings as a new version,
preserving audit history:

```powershell
curl.exe -X POST `
  -H "X-TelemetryBridge-Admin-Key: <admin-secret>" -H 'If-Match: "2"' `
  http://localhost:8084/api/configuration/rollback/1
```

If an ETag is stale, the API returns `409`; reload rather than overwriting another operator.
The file store is appropriate for the single-host Docker reference. Production should use an
authenticated, encrypted, highly available configuration store with the same concurrency and
audit semantics.

## Promotion gates

Validate public/legacy/modern contracts, compare normalized route latency/error/success, check
fallback and shadow errors, and confirm no duplicate side effects. Increase rollout in small
steps. Roll back on SLO or compatibility breach without changing clients. Retire a legacy
operation only after its traffic is zero for the approved evidence window.
