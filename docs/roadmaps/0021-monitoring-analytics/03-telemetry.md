# 0021 · Telemetry in the .NET services

## Options (section `Telemetry`)

| Key | Default | Env var |
|---|---|---|
| `Enabled` | `false` | `UNSHACKLEDWORD_TELEMETRY__ENABLED` |
| `OtlpEndpoint` | `http://unshackledword.monitoring:4317` | `UNSHACKLEDWORD_TELEMETRY__OTLPENDPOINT` |
| `ServiceName` | set per executable (`unshackledword-webapi`, `-mcp`, `-agentworker`) | — |
| `TraceSampleRatio` | `1.0` (scale stays small) | `UNSHACKLEDWORD_TELEMETRY__TRACESAMPLERATIO` |

When `Enabled=false`, `AddUnshackledTelemetry` registers nothing. With FluentValidation at startup: if `Enabled=true`, the endpoint must be an absolute URI and the ratio must be in (0, 1].

## What is collected

```
WebApi / McpServer / AgentWorker
  ├─ traces   AspNetCore (server spans; health + OpenAPI filtered out)
  │           HttpClient (agent provider calls: Anthropic, Gemini, OpenAI-compatible, Ollama)
  │           Npgsql (one span per Dapper command, db.statement = SQL text, no parameters)
  │           ActivitySource "UnshackledWord.AgentMapping" (run → package → attempt)
  ├─ metrics  AspNetCore + HttpClient + runtime (GC, thread pool), Npgsql pool metrics
  └─ logs     Serilog → Serilog.Sinks.OpenTelemetry (trace id/span id on each event)
        └── OTLP gRPC ──▶ unshackledword.monitoring:4317 (otel-lgtm collector)
```

- Npgsql records SQL text but not parameter values (Npgsql's default, which is kept). Search terms and e-mail addresses stay out of Tempo.
- Agent spans carry `run.id`, `package.id`, `profile`, `model` and token counts as attributes. API keys and prompt text are never attributes.
- The console and file sinks stay. Telemetry is added on top, so `docker compose logs` keeps working without the stack.

## Packages (to confirm in chat in 0021.1)

`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, `Npgsql.OpenTelemetry`, `Serilog.Sinks.OpenTelemetry`, `AspNetCore.HealthChecks.NpgSql`. All are Apache 2.0 or MIT. Versions go into `Directory.Packages.props`.

## Health endpoints

| Route | Checks | 200 | 503 |
|---|---|---|---|
| `GET api/health/live` | process only | always while the process serves | — |
| `GET api/health/ready` | Npgsql `SELECT 1` (2 s timeout) and the latest migration script number in the DB ≥ the one the build expects | DB reachable and migrated | DB down or migration pending |

- Both are anonymous, excluded from the 0015 limiter (partition `null` for `/api/health/*`) and filtered out of AspNetCore tracing.
- The response body is the standard health JSON with status per check and no exception text, because the endpoint is public.
- McpServer gets `/health/live` and `/health/ready` the same way (it reads Postgres directly).
