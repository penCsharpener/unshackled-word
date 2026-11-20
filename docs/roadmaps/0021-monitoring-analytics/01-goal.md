# 0021 · Goal

## Today

- The .NET services log through Serilog to the console and to an ephemeral file inside the container (`appsettings.json`: Console, File, Async sinks). No traces, no metrics, no OpenTelemetry package in `Directory.Packages.props`.
- There is no health endpoint. `docs/deployment.md` §8 tells operators to poll an anonymous read endpoint, and §11 lists "a health endpoint" as an open item.
- Nobody is told when the API is down, slow, or throwing 500s. The 0015 rate limiter logs rejections, but nobody reads them unless they grep the logs.
- Agent runs already store their tokens and cost (`AgentRuns.InputTokens/CachedInputTokens/OutputTokens/CostUsd`, `AgentPackages.CostUsd/Calls`, script 0028). Proposals have a status and reviewer (`AgentVerseProposals`), and mappings have `CreatedByUserId`. These numbers can only be seen one run at a time on the maintainer pages.
- There is no visitor analytics, so we don't know which books, features or slider levels readers use. There is also no privacy page ("Datenschutz") in the SPA.

## End state

- `Telemetry:Enabled=true` makes WebApi, McpServer and AgentWorker send traces, metrics and logs over OTLP. Off by default, so a self-hoster who doesn't set it sees no change.
- `api/health/live` and `api/health/ready` (the second checks Postgres) are anonymous, never rate-limited and not traced.
- An optional compose layer `compose.monitoring.yml` adds Grafana with Loki, Tempo and Prometheus (one `grafana/otel-lgtm` container), postgres_exporter, Uptime Kuma and Umami. Grafana, Uptime Kuma and Umami are reached through the existing reverse proxy, each with its own login.
- Grafana has dashboards checked into the repo for: API traffic (rate, errors, latency), Postgres (connections, slow statements), and "Mapping & Agents" (cost per day and model, proposals by status, AI vs. human mappings). The domain numbers come from read-only views on the existing tables.
- Alerts go by email through the existing SMTP settings: API not ready, 5xx spike, an agent run failed or exceeded its budget.
- Cookieless Umami page views plus a few custom events from the SPA. Maintainer and admin pages are not tracked, and search text is never sent. A privacy page explains all of this.

## Out of scope

- Frontend error tracking (GlitchTip/Sentry SDK): deferred, see [07-risks.md](07-risks.md).
- Host metrics (node_exporter, disk, CPU): deferred.
- Single sign-on from the app's accounts into Grafana or Umami.
- Long-term metrics storage, HA, a separate monitoring host.
- Analytics on the obsolete Blazor BibleTagger or on the old frontends.
