# Local collector mode

The local Docker Compose stack uses `../base/otel-collector.yaml`. It accepts OTLP/gRPC and
OTLP/HTTP, removes dangerous attributes, and routes traces to Tempo, metrics to Prometheus,
and logs to Loki. The debug exporter is intentionally enabled only in this local profile.

Production configurations must add TLS and receiver authentication, remove the debug
exporter, bind diagnostic endpoints to a private interface, and use persistent queues.
