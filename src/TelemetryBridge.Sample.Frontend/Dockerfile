# syntax=docker/dockerfile:1
FROM node:20-alpine AS build
WORKDIR /source
COPY package.json package-lock.json ./
COPY src/TelemetryBridge.Browser/package.json src/TelemetryBridge.Browser/package.json
COPY src/TelemetryBridge.Sample.Frontend/package.json src/TelemetryBridge.Sample.Frontend/package.json
RUN --mount=type=cache,id=telemetrybridge-npm,target=/root/.npm npm ci
COPY src/TelemetryBridge.Browser src/TelemetryBridge.Browser
COPY src/TelemetryBridge.Sample.Frontend src/TelemetryBridge.Sample.Frontend
ARG VITE_API_ORIGIN=http://localhost:8080
ARG VITE_OTLP_TRACES_URL=http://localhost:4318/v1/traces
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY src/TelemetryBridge.Sample.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/src/TelemetryBridge.Sample.Frontend/dist /usr/share/nginx/html
EXPOSE 8080
