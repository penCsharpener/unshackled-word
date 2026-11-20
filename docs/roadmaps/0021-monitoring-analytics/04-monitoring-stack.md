# 0021 · Monitoring stack

## `compose.monitoring.yml` (layered on `compose.yml`, opt-in)

```
docker compose -f compose.yml -f compose.monitoring.yml up -d

unshackledword.monitoring   grafana/otel-lgtm:<pinned>   OTLP 4317/4318 (compose network only), Grafana 3000
postgres-exporter           prometheuscommunity/postgres-exporter:<pinned>   scraped by the collector
uptime-kuma                 louislabs/uptime-kuma:<pinned 2.x>               3001
umami                       ghcr.io/umami-software/umami:<pinned postgresql tag>  3000 → 3002
```

- Image tags are pinned (no `latest`). Each image is named in chat before it is added.
- No ports are published on public interfaces: `127.0.0.1:` bindings for local access, and the proxy reaches the services by service name.
- Volumes: `./temp/monitoring/lgtm` (Grafana, Loki, Tempo, Prometheus data), `./temp/monitoring/kuma`. Umami stores its data in Postgres.
- Retention: Loki and Tempo 14 days, Prometheus 30 days. Configured by mounting config files from `deploy/monitoring/`.
- The collector inside otel-lgtm gets an extra `prometheus` receiver job for `postgres-exporter:9187`, from a mounted collector config. If the image doesn't allow this cleanly, fall back to separate `prometheus` and `grafana` containers (see risks).

## Postgres metrics

- `compose.monitoring.yml` adds `command: ["postgres", "-c", "shared_preload_libraries=pg_stat_statements"]` to the database service. That takes one DB restart.
- Migration `0036_Monitoring.sql`:
  - `CREATE EXTENSION IF NOT EXISTS pg_stat_statements`, only inside a `DO` block that checks `current_setting('shared_preload_libraries')`, so migrations stay green without the preload.
  - Creates the roles `uw_monitor` (member of `pg_monitor`, for the exporter) and `uw_grafana` (`USAGE` on schema `monitoring`, `SELECT` on its views only). Both are created `NOLOGIN`. The operator runs `ALTER ROLE … LOGIN PASSWORD …` once (documented), so no password is ever in the repo.

## Monitoring views (schema `monitoring`, also in 0036)

| View | Columns | Source |
|---|---|---|
| `AgentCostDaily` | day, profile, model, runs, input/cached/output tokens, cost_usd | `AgentRuns` |
| `AgentRunOutcomes` | id, status, profile, model, created_at, finished_at, cost_usd, max_cost_usd | `AgentRuns` (no user ids, no status notes) |
| `ProposalStatusDaily` | day, language, status, count | `AgentVerseProposals` |
| `MappingAuthorship` | language, book, ai_links, human_links | `Elb1871GreekMapping`/`Elb1871HebrewMapping` by `CreatedByUserId` joined to AI users from `AgentRuns.AiUserId` |
| `RevisionActivityDaily` | day, action, count | `RevisionVerseAudit` (no author) |

Grafana's Postgres datasource uses `uw_grafana`, so it can't see `Users`, `AuthTokens`, `PasskeyCredentials`, `ApiTokens` or `AdminLog`.

## Dashboards and alerts (provisioned from `deploy/monitoring/grafana/`)

- Dashboards as JSON, provisioned read-only: **API** (requests/s, 5xx %, p50/p95 per route, 429 count, trace links), **Postgres** (connections vs. `max_connections`, top statements by total time, DB size), **Mapping & Agents** (views above).
- Contact point `email` from the `EMAIL_*` env vars (`GF_SMTP_*`), recipient `MONITORING_ALERT_EMAIL`.
- Alert rules: 5xx > 2 % of requests over 10 min (min. 20 requests); p95 > 2 s over 10 min; any `AgentRunOutcomes` row turning `failed`/`budget-exceeded` in the last 15 min; connections > 80 % of max.
- Uptime Kuma: HTTP monitors for `https://<site>/api/health/ready` (60 s) and `https://<site>/` (SPA), SMTP notification to the same address. "API down" comes from Kuma, not Grafana, so it still fires when the lgtm container is down.

## Proxy (deployment.md §4.3 / Caddyfile)

| Host | Target | Auth |
|---|---|---|
| `monitor.<domain>` | `unshackledword.monitoring:3000` | Grafana login (`GF_SECURITY_ADMIN_PASSWORD` from `.env`, anonymous off, sign-up off) |
| `status.<domain>` | `uptime-kuma:3001` | Kuma login; a public status page is optional |
| `analytics.<domain>` | `umami:3000` | Umami login |
| `<domain>/stats/*` | `umami:3000` (script + collect only, see [05-analytics.md](05-analytics.md)) | public |

Assumption: subdomains are available. Sub-paths would also work for Grafana (`GF_SERVER_SERVE_FROM_SUB_PATH`) but not reliably for Uptime Kuma.
