# Roadmap 0015: Rate limit for anonymous callers

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions and code findings the plan rests on |
| [03-limiter-design.md](03-limiter-design.md) | Pipeline order, partitioning, tiers, 429 response, frontend handling |
| [04-config.md](04-config.md) | `RateLimiting` and `ReverseProxy` settings, env vars, defaults |
| [05-tasks.md](05-tasks.md) | Task table (backend and frontend woven) |
| [06-risks.md](06-risks.md) | Risks worth naming before starting |

## Status (2026-11-15)

All rows 0015.1–0015.7 implemented (not committed). Verified by real API runs with curl: general/search/auth 429s with body and `Retry-After`, session cookie exempt, disabled = no limit, invalid options stop startup, forwarded headers per client, Production warning; anonymous 401 / plain user 403 on data quality, mapping save and tagger writes. `dotnet test` (WebApi 133, Application 414) and `bun run check` (531 tests) green.

Not verified: maintainer side (200 on data quality, `editingEnabled: true`, mapping save) and the notice in a real browser.

Deviations:
- `Retry-After` is one window segment (10 s by default), not the time until a permit is surely free; the UI treats it as a hint ("Neu laden" may hit 429 again).
- A request rejected by the strict tier has already used a general permit (chain order general → strict).
- The 429 `tier` is found from the strict limiter's statistics, since `OnRejected` does not say which limiter refused.
- `Auth/Login` keeps its older FastEndpoints `Throttle(10/min)` on top of the auth tier.
- Magic-link login and e-mail verification pages (token links) still show their generic error on 429.
