# Roadmap 0021: Monitoring and analytics

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions and code findings the plan rests on |
| [03-telemetry.md](03-telemetry.md) | OpenTelemetry in the .NET services, `Telemetry` options, health endpoints |
| [04-monitoring-stack.md](04-monitoring-stack.md) | `compose.monitoring.yml`, Postgres metrics, monitoring views, dashboards, alerts, proxy |
| [05-analytics.md](05-analytics.md) | Umami: database, same-origin tracker, custom events, privacy page |
| [06-tasks.md](06-tasks.md) | Task table (backend and frontend woven) |
| [07-risks.md](07-risks.md) | Risks worth naming before starting |

## Status (2026-11-20)

Implemented in 0.7.0, not committed. All rows 0021.1–0021.11 have code, config or docs. The "Done when" checks that need the running stack (Tempo traces, Loki logs, alert mails, Kuma down/up mails, Umami events, privacy page on devices) have not been run yet.

Deviations and decisions:
- No `AspNetCore.HealthChecks.NpgSql`: own `PostgresHealthCheck` (`SELECT 1`, 2 s) and `MigrationsHealthCheck` (DbUp `schemaversions` ≥ `ExpectedMigration`, kept in sync with `scripts/` by a test).
- Query strings are stripped from all spans (`SpanSanitizer`), maintainer decision.
- Umami database is not backed up (maintainer decision).
- The proxy strips `/stats` (nginx `deployment/nginx.prod.conf`; Vite dev proxy), so Umami runs with its default `TRACKER_SCRIPT_NAME`/`COLLECT_API_ENDPOINT`. Production proxy is nginx behind Nginx Proxy Manager, not Caddy.
- OTLP ports 4317/4318 are published on `127.0.0.1` because the dev API runs with host networking.
- `uwBeforeSend` excludes `/wartung`, `/admin`, `/edit` (the maintainer pages live under `/edit`); the staff flag is remembered in `localStorage` so the first page view after a reload is dropped too. The tracker tag is injected at the end of `<body>` so it runs after the app module.
- Agent worker has no compose service, so its telemetry env vars are set where it is run.
