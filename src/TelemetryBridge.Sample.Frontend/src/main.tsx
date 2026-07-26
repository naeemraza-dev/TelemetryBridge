import React from "react";
import ReactDOM from "react-dom/client";
import { initializeTelemetry } from "@telemetry-bridge/browser";
import App from "./App";
import "./styles.css";

const apiOrigin = import.meta.env.VITE_API_ORIGIN ?? "http://localhost:8080";

initializeTelemetry({
  serviceName: "telemetrybridge-sample-frontend",
  serviceVersion: "1.0.0",
  environment: import.meta.env.MODE,
  otlpEndpoint: import.meta.env.VITE_OTLP_TRACES_URL ?? "http://localhost:4318/v1/traces",
  allowedTraceOrigins: [new RegExp(`^${apiOrigin.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}`)],
  samplingRatio: Number(import.meta.env.VITE_TRACE_SAMPLING_RATIO ?? "1")
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App apiOrigin={apiOrigin} />
  </React.StrictMode>
);
