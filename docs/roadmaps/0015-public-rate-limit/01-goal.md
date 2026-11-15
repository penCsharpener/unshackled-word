# 0015 · Goal

## Today

- The WebApi has no rate limiting. Every endpoint without `[RequiresLogin]` is made anonymous in `Program.cs` (`Endpoints.Configurator` → `AllowAnonymous()`): 34 of 76 endpoints, including the search endpoints (`search/verses`, `search/originals`, `strongsOccurrences`) and every login/registration endpoint (`login`, `register`, `request-magic-link`, `request-password-reset`, `passkeys/login/*` ...).
- Anyone can script the public reads at any speed, and the auth endpoints can be hit without limit (password guessing, mail flooding via magic link / password reset).
- Six endpoints are anonymous and write or serve tagging data: `Elberfelder/SaveVerseMapping` (PUT, gated only by `MappingEditing:Enabled`) and `BibleTagger/*` (`mapping/create`, `grammar`, `dashboard/backup/mappings` write; two tagging GETs read), called only by the obsolete Blazor BibleTagger.
- The data quality pages (`DataQuality/*`, 9 read endpoints) need only a login, no role; only `DataQuality/Refresh` checks `Admin`/`Maintainer`. The web menu shows "Datenqualität" to every logged-in user (`UserMenu.tsx`).

## End state

- A config switch `RateLimiting:Enabled` (appsettings or `UNSHACKLEDWORD_RATELIMITING__ENABLED`) turns on a per-client-IP limit for anonymous callers. Off by default; when off, nothing changes.
- Two tiers, all numbers configurable: a general limit for every anonymous request, and a strict limit for expensive search endpoints and auth endpoints (separate counters, see [02-decisions.md](02-decisions.md)).
- Logged-in users are never limited.
- Behind the production reverse proxy the real client IP is used (trusted proxies from config).
- An anonymous reader who hits the limit sees a German/English notice with the wait time and a login link, not a generic error.
- Data quality (pages and endpoints) is maintainer-only (`Admin`, `Maintainer`).
- No endpoint writes data without a login: the mapping save and the old tagger writes require `Admin`/`Maintainer`.

## Out of scope

- The MCP server (separate service, reads Postgres directly; its access policy is still undecided).
- A suggestion flow for non-maintainers ("many suggest, maintainer merges"): until it exists, only maintainers write.
- Distributed counters (Redis): one WebApi instance, in-memory counters are enough (scale stays small).
- Per-user quotas, API keys, rate-limit dashboards.
