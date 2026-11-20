# 0021 · Decisions

## Maintainer decisions (2026-11-20)

- **Open source only.** Seq (proprietary) and the Sentry server (FSL) are excluded. Chosen: OpenTelemetry .NET (Apache 2.0), `grafana/otel-lgtm` (Grafana, Loki, Tempo: AGPL; Prometheus, OTel Collector: Apache), postgres_exporter (Apache), Uptime Kuma (MIT), Umami (MIT). These run as separate services, so the app's CC0 licence is not affected.
- **Same host, behind the proxy.** The monitoring stack runs on the app host as an extra compose file. Grafana, Uptime Kuma and Umami are reached through the existing reverse proxy, each with its own login.
- **In scope:** Umami, Uptime Kuma, Postgres metrics. **Not now:** GlitchTip.
- **Alerts by email** through the SMTP account the API already uses (`EMAIL_*` in `.env`).
- **Domain numbers come from Postgres, not from code.** Grafana reads read-only views. No OTel business meters, because the tables already hold the full history (cost per package since 0028) and meters would duplicate it, starting from zero.
- **Approving new dependencies.** CLAUDE.md says no dependency is added without asking. Accepting this roadmap is not that approval: 0021.1 and 0021.9f each start by naming their exact packages or images in chat.

## Consequences

- One code path for telemetry, shared by three executables: `Infrastructure/Observability/TelemetryExtensions.cs` (`AddUnshackledTelemetry(builder, serviceName)`).
- Grafana never connects to the app database as the owner. It uses a login role that can only `SELECT` from the `monitoring` schema views. The views contain no e-mail addresses, tokens or IPs ([04-monitoring-stack.md](04-monitoring-stack.md)).
- Umami gets its own database (`umami`) and its own user on the same Postgres server. Nothing is shared with the `unshackled-word` schema.
- The tracker is served through the main origin (`/stats/*` proxied to Umami), so there is no third-party request, no CORS, and ad blockers don't block it as a known host.

## Findings the plan rests on

- `Program.cs` in WebApi and McpServer call `AddSerilog(... ReadFrom.Configuration ...)`. A Serilog OTLP sink can be added through config, without touching the logging calls.
- The API image is `mcr.microsoft.com/dotnet/aspnet:11.0`, which has no `curl`/`wget`. A compose `healthcheck` against `/api/health/ready` needs a tool in the image. Uptime Kuma does the external check instead (see risks).
- The 0015 limiter is built in `RateLimiting/RateLimitingExtensions.cs` with `PartitionedRateLimiter.Create`. Health endpoints have to be excluded there, otherwise Uptime Kuma's polling uses up the general per-IP budget.
- Postgres runs from the stock `postgres:18` image with an existing volume. Init scripts in `/docker-entrypoint-initdb.d` don't run again, so the `umami` database and the roles are created by a migration script or a documented one-time step.
- `pg_stat_statements` needs `shared_preload_libraries`, which takes a Postgres restart. The migration may only `CREATE EXTENSION` when the library is loaded, otherwise migrations break for self-hosters without it.
