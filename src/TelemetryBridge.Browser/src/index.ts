import {
  SpanStatusCode,
  context,
  trace,
  type Attributes,
  type Span,
  type Tracer
} from "@opentelemetry/api";
import { ZoneContextManager } from "@opentelemetry/context-zone";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { DocumentLoadInstrumentation } from "@opentelemetry/instrumentation-document-load";
import { FetchInstrumentation } from "@opentelemetry/instrumentation-fetch";
import { UserInteractionInstrumentation } from "@opentelemetry/instrumentation-user-interaction";
import { XMLHttpRequestInstrumentation } from "@opentelemetry/instrumentation-xml-http-request";
import { resourceFromAttributes } from "@opentelemetry/resources";
import {
  AlwaysOnSampler,
  BatchSpanProcessor,
  ParentBasedSampler,
  TraceIdRatioBasedSampler
} from "@opentelemetry/sdk-trace-base";
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import {
  ATTR_DEPLOYMENT_ENVIRONMENT_NAME,
  ATTR_SERVICE_NAME,
  ATTR_SERVICE_VERSION
} from "@opentelemetry/semantic-conventions";

const maximumAttributeLength = 256;
const allowedCustomAttributes = new Set([
  "telemetrybridge.feature.name",
  "telemetrybridge.operation.type",
  "telemetrybridge.workflow.name"
]);

/** Secure browser SDK initialization options. */
export interface TelemetryBrowserOptions {
  serviceName: string;
  serviceVersion?: string;
  environment: string;
  otlpEndpoint: string;
  allowedTraceOrigins: Array<string | RegExp>;
  samplingRatio?: number;
}

let browserTracer: Tracer | undefined;

/** Initializes browser tracing once and returns the application tracer. */
export function initializeTelemetry(options: TelemetryBrowserOptions): Tracer {
  if (browserTracer) {
    return browserTracer;
  }

  validateOptions(options);
  const exporter = new OTLPTraceExporter({ url: options.otlpEndpoint });
  const ratio = options.samplingRatio ?? 1;
  const provider = new WebTracerProvider({
    resource: resourceFromAttributes({
      [ATTR_SERVICE_NAME]: options.serviceName,
      [ATTR_SERVICE_VERSION]: options.serviceVersion ?? "0.0.0",
      [ATTR_DEPLOYMENT_ENVIRONMENT_NAME]: options.environment
    }),
    sampler: ratio === 1
      ? new ParentBasedSampler({ root: new AlwaysOnSampler() })
      : new ParentBasedSampler({ root: new TraceIdRatioBasedSampler(ratio) }),
    spanProcessors: [new BatchSpanProcessor(exporter)]
  });

  provider.register({ contextManager: new ZoneContextManager() });
  registerInstrumentations({
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        propagateTraceHeaderCorsUrls: options.allowedTraceOrigins,
        clearTimingResources: true,
        ignoreUrls: [options.otlpEndpoint]
      }),
      new XMLHttpRequestInstrumentation({
        propagateTraceHeaderCorsUrls: options.allowedTraceOrigins,
        ignoreUrls: [options.otlpEndpoint]
      }),
      new UserInteractionInstrumentation({
        eventNames: ["click", "submit"]
      })
    ]
  });

  browserTracer = trace.getTracer(options.serviceName, options.serviceVersion);
  installRouteChangeTracing(browserTracer);
  return browserTracer;
}

/** Runs an async business action in a stable, controlled custom span. */
export async function traceAction<T>(
  name: "ui.order.submit" | "ui.customer.search" | "ui.login.start" | "ui.report.generate",
  action: (span: Span) => Promise<T>,
  attributes: Attributes = {}
): Promise<T> {
  if (!browserTracer) {
    throw new Error("initializeTelemetry must be called before traceAction.");
  }

  return browserTracer.startActiveSpan(name, async (span) => {
    try {
      for (const [key, value] of Object.entries(sanitizeAttributes(attributes))) {
        if (value !== undefined) {
          span.setAttribute(key, value);
        }
      }
      return await context.with(trace.setSpan(context.active(), span), () => action(span));
    } catch (error) {
      if (error instanceof Error) {
        span.recordException(error);
        span.setStatus({ code: SpanStatusCode.ERROR, message: error.name });
      }
      throw error;
    } finally {
      span.end();
    }
  });
}

/** Removes query strings and fragments without retaining their values. */
export function removeQueryString(value: string): string {
  const index = value.search(/[?#]/);
  return index < 0 ? value : value.slice(0, index);
}

/** Keeps only controlled custom attributes and bounded primitive values. */
export function sanitizeAttributes(attributes: Attributes): Attributes {
  const result: Attributes = {};
  for (const [key, value] of Object.entries(attributes)) {
    if (!allowedCustomAttributes.has(key)) {
      continue;
    }

    if (typeof value === "string") {
      result[key] = removeQueryString(value).slice(0, maximumAttributeLength);
    } else if (value !== undefined) {
      result[key] = value;
    }
  }
  return result;
}

function validateOptions(options: TelemetryBrowserOptions): void {
  if (!options.serviceName.trim()) {
    throw new Error("serviceName is required.");
  }
  if (!options.environment.trim()) {
    throw new Error("environment is required.");
  }
  if (!options.otlpEndpoint.trim()) {
    throw new Error("otlpEndpoint is required.");
  }
  const ratio = options.samplingRatio ?? 1;
  if (ratio < 0 || ratio > 1) {
    throw new Error("samplingRatio must be between 0 and 1.");
  }
}

function installRouteChangeTracing(tracer: Tracer): void {
  const record = (): void => {
    tracer.startActiveSpan("ui.route.change", (span) => span.end());
  };
  window.addEventListener("popstate", record);

  for (const method of ["pushState", "replaceState"] as const) {
    const original = history[method];
    history[method] = function (this: History, ...args: Parameters<History[typeof method]>): void {
      original.apply(this, args);
      record();
    } as History[typeof method];
  }
}
