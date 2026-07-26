ARG BASE_IMAGE
FROM busybox:1.37.0-musl AS health-tools

ARG BASE_IMAGE
FROM ${BASE_IMAGE}

# Grafana's Loki and Tempo images are intentionally minimal and contain no
# shell or HTTP client. Add only the static BusyBox binary for local readiness
# probes; the upstream entrypoint and command remain unchanged.
COPY --from=health-tools /bin/busybox /bin/busybox
