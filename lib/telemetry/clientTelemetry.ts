"use client";

import { analytics } from "@/lib/analytics";

const CORRELATION_STORAGE_KEY = "weup.observability.correlation";
const CORRELATION_HEADER = "X-Correlation-ID";

type TelemetryWindow = Window & {
  __weupTelemetryInstalled?: boolean;
};

function getOrCreateCorrelationId(): string {
  if (typeof window === "undefined") {
    return "server";
  }

  try {
    const existing = window.localStorage
      .getItem(CORRELATION_STORAGE_KEY)
      ?.trim();
    if (existing) {
      return existing;
    }

    const nextId = crypto.randomUUID();
    window.localStorage.setItem(CORRELATION_STORAGE_KEY, nextId);
    return nextId;
  } catch {
    return crypto.randomUUID();
  }
}

function describeInput(input: RequestInfo | URL): string {
  if (typeof input === "string") {
    return input;
  }

  if (input instanceof URL) {
    return input.toString();
  }

  return input.url;
}

export function installClientTelemetry(): void {
  if (typeof window === "undefined") {
    return;
  }

  const telemetryWindow = window as TelemetryWindow;
  if (telemetryWindow.__weupTelemetryInstalled) {
    return;
  }

  telemetryWindow.__weupTelemetryInstalled = true;

  const correlationId = getOrCreateCorrelationId();
  const originalFetch = window.fetch.bind(window);

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const headers = new Headers(
      init?.headers ?? (input instanceof Request ? input.headers : undefined),
    );
    if (!headers.has(CORRELATION_HEADER)) {
      headers.set(CORRELATION_HEADER, correlationId);
    }

    const nextInit: RequestInit = {
      ...init,
      headers,
    };

    try {
      const response = await originalFetch(input, nextInit);
      if (response.status >= 500) {
        void analytics.track({
          name: "frontend_exception",
          props: {
            kind: "http_5xx",
            status: response.status,
            url: describeInput(input),
          },
        });
      }

      return response;
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "unknown fetch error";
      void analytics.track({
        name: "frontend_exception",
        props: {
          kind: "fetch_failure",
          message,
          url: describeInput(input),
        },
      });
      throw error;
    }
  };

  window.addEventListener("error", (event) => {
    void analytics.track({
      name: "frontend_exception",
      props: {
        kind: "window_error",
        message: event.message || "unhandled window error",
        source: event.filename || "unknown",
      },
    });
  });

  window.addEventListener("unhandledrejection", (event) => {
    const reason = event.reason;
    const message =
      reason instanceof Error
        ? reason.message
        : String(reason ?? "unknown promise rejection");

    void analytics.track({
      name: "frontend_exception",
      props: {
        kind: "unhandled_rejection",
        message,
      },
    });
  });
}
