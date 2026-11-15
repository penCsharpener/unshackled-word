# 0015 · Decisions

## Maintainer decisions (2026-11-15)

- **Toggle from config.** `RateLimiting:Enabled`, default `false`; env var `UNSHACKLEDWORD_RATELIMITING__ENABLED`. When `false` the limiter middleware is not registered at all.
- **Only anonymous callers are limited.** A request with a valid session cookie gets no limiter, on any endpoint.
- **Two tiers.** General (every anonymous request) and strict (expensive search endpoints, auth endpoints). Numbers per tier from config.
- **Behind a reverse proxy in production.** Client IP comes from `X-Forwarded-For`, trusted only from configured proxies/networks.
- **429 in the UI:** notice with wait time and login hint, German and English.
- **MCP server out of scope.**
- **Anonymous writes are closed in this roadmap too** (added 2026-11-15, after the first draft).
- **Data quality and exports are maintainer-only.** `ExportSeed` already requires `Admin`/`Maintainer`; the data quality endpoints get the same roles. Client-side CSV downloads live on the data quality page and follow it.

## Consequences (architect's calls, not asked)

- **Strict tier has one counter per group, not one shared counter.** Groups `search` and `auth` use the same configured numbers but separate partitions. Otherwise a reader who used up search could not log in, and logging in is exactly the way out the 429 notice offers.
- **Built-in ASP.NET Core rate limiting** (`Microsoft.AspNetCore.RateLimiting`, part of the shared framework, already referenced): no new package. FastEndpoints' own `Throttle()` is not used: it is per endpoint, identifies clients by a header without proxy trust, and knows nothing about login.
- **Sliding window** for both tiers: no burst doubling at window edges, which a fixed window allows.
- **Every anonymous request counts toward the general tier**; a strict-tier request counts toward both (chained limiter). Simple to reason about: strict is a sub-budget of general.
- **The 429 body carries `retryAfterSeconds`** in addition to the `Retry-After` header, because the production CORS policy does not expose response headers to the SPA.

- **`SaveVerseMapping` requires `Admin`/`Maintainer`**, like `Revision/SaveVerseMapping`; `MappingEditing:Enabled` stays as an extra server-wide switch. `GetVerseMapping`'s `editingEnabled` becomes "setting on **and** caller has a write role", so the React `MappingEditor` turns read-only by itself for everyone else.
- **The three `BibleTagger/*` writes require `Admin`/`Maintainer`.** Their only caller is the obsolete Blazor BibleTagger, which has its own Identity and sends no API session cookie: in effect its writes stop working. Accepted, since EF Core/BibleTagger is obsolete; the two `BibleTagger` GETs stay public reads in the general tier.

## Findings the plan rests on

- Cookie authentication runs in `UseAuthentication()` before `UseAuthorization()` (`Program.cs`); `HttpContext.User` is populated before the limiter if `UseRateLimiter()` sits between them. The cookie is data-protected and re-validated (`OnValidatePrincipal` in `AuthServiceCollectionExtensions.cs`), so a forged cookie does not grant the exemption.
- `Roles(...)` alone does not protect an endpoint here: `Endpoints.Configurator` calls `AllowAnonymous()` on every endpoint class without `[RequiresLogin]`. Every newly gated endpoint needs both (all existing role-checked endpoints, e.g. `Revision/SaveVerse`, `Maintainer/ExportSeed`, have both).
- Roles are claims (`Session.cs`, `ClaimTypes.Role`), checked with FastEndpoints `Roles(AppRoles.Admin, AppRoles.Maintainer)`; the frontend mirror is `hasWriteRole` in `lib/auth/roles.ts`.
- Endpoint opt-ins already use a class attribute read in `Endpoints.Configurator` (`[RequiresLogin]`); the strict tier follows the same pattern.
- `compose.yml` publishes the WebApi on `7654:8080`; there is no proxy service in compose, the proxy lives outside it.
