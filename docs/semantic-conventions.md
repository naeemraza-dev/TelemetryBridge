# Semantic conventions

TelemetryBridge follows current stable OpenTelemetry names exposed by the selected SDK and
keeps platform-owned attributes under `telemetrybridge.*`.

Use stable semantic attributes such as `service.name`, `service.namespace`,
`service.version`, `deployment.environment.name`, `http.request.method`, normalized
`http.route`, `http.response.status_code`, `server.address`, `db.system.name`,
`db.operation.name`, `messaging.system`, and `messaging.operation.type`.

Platform-owned low-cardinality attributes include:

- `telemetrybridge.operation.type`
- `telemetrybridge.workflow.name`
- `telemetrybridge.modernization.target`
- `telemetrybridge.shadow.outcome`

Do not copy deprecated HTTP/DB names into custom spans to imitate instrumentation. During an
SDK/Collector upgrade, inspect emitted names, update dashboards/alerts together, validate all
Collector configurations with that exact binary, and run contract and E2E tests. Attribute
renames are schema migrations and require the same rollout discipline as API changes.

Database command text and parameters are off by default. The development-only parameterized
text switch is rejected outside `Development`, and the base Collector deletes `db.query.text`
and `db.statement` as defense in depth.
