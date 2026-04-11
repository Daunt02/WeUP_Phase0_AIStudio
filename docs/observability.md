# Observability for WeUP (P22)

This document describes the observability architecture added in Phase 0: OpenTelemetry tracing, structured logging, and correlation IDs across frontend and backend seams.

## Architecture

- Traces: OpenTelemetry Tracing is configured in the `WeUP.Api` project. Instrumentation includes ASP.NET Core requests, outgoing `HttpClient` calls, and EF Core database calls. Traces are exported to Console (dev) and optionally OTLP endpoint when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- Logs: Serilog is configured to emit structured JSON logs to Console. Logs include a stable set of fields and include correlation metadata via logging scopes.
- Correlation: A correlation ID header (`X-Correlation-ID`) is used for inbound requests and propagated to outgoing HTTP calls and background workflows. The middleware generates and resolves IDs and stores them on `HttpContext.Items` and `Activity` tags.

## Key components

- `WeUP.Api.Observability.ObservabilityExtensions` — configures Serilog and OpenTelemetry, registers correlation handler and activity sources.
- `CorrelationIdMiddleware` — ensures inbound requests have a correlation ID, sets response header, adds logging scope and Activity tag.
- `CorrelationIdDelegatingHandler` — attaches correlation ID to outgoing `HttpClient` requests.
- `LoggingExtensions` — helper structured logging methods (ingestion, moderation, etc.).

## Correlation strategy

- Inbound: The middleware reads `X-Correlation-ID` header; if missing it generates a GUID. It sets the header on the response and adds the ID to the current `Activity` as `correlation_id` tag.
- Propagation: Outgoing `HttpClient` calls use `CorrelationIdDelegatingHandler` to copy the correlation ID from `HttpContext.Items` or `Activity` into the outgoing request header.
- Logs: The middleware begins a logging scope containing `CorrelationId` so every `ILogger` entry in that scope includes the correlation value.
- Background jobs: If a background job is initiated from a request, code should create an `ActivitySource` activity and set the `correlation_id` tag to the originating ID so traces link.

## Structured log schema

All structured logs include the following stable fields (rendered as JSON):

- `Timestamp` (automatic)
- `Level` (Info/Warn/Error)
- `MessageTemplate` (string)
- `RenderedMessage` (string)
- `Application` = "WeUP"
- `CorrelationId` (from middleware scope)
- `TraceId` (if available via OpenTelemetry/Activity)
- `SpanId` (if available)
- `EventId` (numeric event id grouping)
- domain fields vary by event (e.g., `IngestionId`, `Source`, `ItemId`, `UserId`)

Example event: Ingestion started

{
"Timestamp": "...",
"Level": "Information",
"Application": "WeUP",
"CorrelationId": "abcd-...",
"TraceId": "...",
"EventId": 1000,
"MessageTemplate": "Ingestion started: {IngestionId} from {Source}",
"IngestionId": "ing-123",
"Source": "manual"
}

## Frontend integration

Add a small helper when making API calls so the browser attaches a `X-Correlation-ID` header. The frontend helper stores a correlation per-tab (localStorage) and forwards it. See `services/correlation.ts`.

## Verification / Smoke tests

1. Start backend locally. Watch console output for JSON logs.
2. Make an API request from the frontend or `curl` without a correlation header. Confirm response contains `X-Correlation-ID` header and that logs for the request include `CorrelationId` and a `TraceId`.
3. Trigger an ingestion action that performs outgoing HTTP calls. Confirm those outgoing calls have `X-Correlation-ID` header set and that traces show linked spans (ingest call spans nested under request span).
4. Induce an error (e.g., invalid input) and confirm exception logs include `CorrelationId` and `TraceId`.

## Environment configuration

- `OTEL_EXPORTER_OTLP_ENDPOINT` — optional OTLP exporter endpoint (OTLP/HTTP). If not set, console exporter is used.
- Sampling and other OpenTelemetry options can be set via `AddOpenTelemetryTracing` configuration in `ObservabilityExtensions`.

## Developer notes and tradeoffs

- We intentionally keep the correlation ID separate from trace ID. Correlation is an application-level ID helpful to tie asynchronous workflows, messages and logs that may not be part of a single trace.
- For production, enable OTLP exporter and route to a collector (Jaeger/Tempo/Datadog) and send logs to a log store (Azure Monitor/Logstash).
- Avoid noisy instrumentation: keep meaningful span boundaries (ingestion start/complete, moderation actions, DB writes).
