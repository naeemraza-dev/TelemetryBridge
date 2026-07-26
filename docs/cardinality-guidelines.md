# Cardinality guidelines

Metric storage cost grows with every unique combination of attributes. Trace and log indexing
also becomes slower and more expensive as uncontrolled values proliferate.

Allowed dimensions include HTTP method, normalized route, status code, stable service identity,
environment, operation type, database system, and controlled feature/workflow names.

Never use trace/span IDs, request IDs, order/invoice IDs, user/tenant IDs, emails, session IDs,
raw URLs, query strings, SQL statements, exception messages, stack traces, or arbitrary input
as metric attributes.

`TelemetryAttributePolicy` applies an explicit custom-attribute allowlist, denylist, URL query
removal, identifier masking, secret-pattern redaction, and bounded string lengths. The browser
package independently allowlists three controlled custom keys. Collector deletion rules provide
a second enforcement layer for every sender.

Adding an attribute requires an owner, purpose, bounded value set, sensitivity classification,
retention recommendation, unit tests, and a volume estimate.

