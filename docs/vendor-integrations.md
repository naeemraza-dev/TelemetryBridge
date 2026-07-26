# Optional backend integrations

Applications always emit OTLP to the Collector. Select an exporter at deployment time.

## Datadog

```powershell
$env:DD_API_KEY="<secret>"
$env:DD_SITE="datadoghq.com"
docker compose -f docker-compose.yml -f docker-compose.datadog.yml up --build
```

The overlay replaces the Collector pipeline with the Datadog exporter. Review
`dashboards/datadog/monitors.json` and replace service/environment scopes. Inject the key from
a secret manager in production. If a Datadog agent or tracer already collects APM/logs, disable
the overlapping signal before enabling Collector export.

## Azure Monitor / Application Insights

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING="<secret>"
docker compose -f docker-compose.yml -f docker-compose.azure-monitor.yml up --build
```

The Azure Monitor exporter handles traces and logs in this configuration. Example queries are
in `dashboards/application-insights/queries.kql`. Do not run the legacy Application Insights
SDK auto-collection and Collector export for the same request/log stream. Inventory request,
dependency, exception, and log modules first, select one owner per signal, validate counts, and
then remove the old path.

## Grafana-compatible local stack

Plain `docker compose up --build` routes traces to Tempo, metrics to Prometheus, and logs to
Loki. Grafana provisions both the overview and operations dashboards. This local mode includes
open HTTP receivers and local credentials and must not be internet-facing.

To fan out to multiple paid backends, create a reviewed Collector config listing each exporter.
Remember that every additional exporter multiplies exported volume. Do not fan out accidentally
during migration; label and measure the temporary duplication window.
