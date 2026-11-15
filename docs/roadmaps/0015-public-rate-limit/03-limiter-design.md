# 0015 · Limiter design

## Pipeline order (`Program.cs`)

```
UseForwardedHeaders()      only when ReverseProxy has entries (04-config.md); must run first
UseCors(...)
UseAuthentication()        fills HttpContext.User from the session cookie
UseRateLimiter()           only when RateLimiting:Enabled
UseAuthorization()
UseFastEndpoints(...)
```

The limiter runs after routing (WebApplication adds `UseRouting` implicitly at the start), so `context.GetEndpoint()` already returns the FastEndpoints endpoint and its metadata. Verify this in 0015.1; if the endpoint is `null` there, add an explicit `app.UseRouting()` before `UseAuthentication()`.

## Partitioning

One `GlobalLimiter`, built with `PartitionedRateLimiter.CreateChained(general, strict)`:

| Limiter | Partition key | Applies to |
|---|---|---|
| general | `general:{clientIp}` | every request where `User.Identity.IsAuthenticated` is false |
| strict | `{group}:{clientIp}` (`search:` / `auth:`) | anonymous requests whose endpoint carries `RateLimitGroupMetadata` |

Authenticated requests, and requests without strict metadata in the strict limiter, get `RateLimitPartition.GetNoLimiter`. The key function is a small pure static class (`RateLimitPartitioner`: in → `isAuthenticated`, `group?`, `ip`; out → key or "no limit") so it is unit-testable without a host.

Client IP: `HttpContext.Connection.RemoteIpAddress` after the forwarded-headers middleware, IPv6 mapped to IPv4 (`MapToIPv4` when `IsIPv4MappedToIPv6`), and IPv6 grouped by `/64` so one host cannot rotate through its own prefix. Missing IP → key `unknown` (shares one bucket, logged once).

## Strict tier opt-in

New attribute `[RateLimitGroup("search")]` / `[RateLimitGroup("auth")]` in `Auth/` next to `RequiresLoginAttribute`. The existing `Endpoints.Configurator` reads it and adds `ep.Options(b => b.WithMetadata(new RateLimitGroupMetadata(group)))`.

| Group | Endpoints |
|---|---|
| `search` | `Elberfelder/SearchVerses`, `Elberfelder/Search/Originals`, `Elberfelder/GetStrongsOccurrences`, `Elberfelder/GetWordStats` |
| `auth` | `Auth/Login`, `Register`, `RequestMagicLink`, `MagicLinkLogin`, `RequestPasswordReset`, `ResetPassword`, `VerifyEmail`, `PasskeyLogin`, `GetPasskeyLoginOptions` |

`RegistrationPolicy` and all other reads stay general only. A search endpoint that turns out cheap moves out by removing its attribute; the list is a starting point, not a measurement.

## 429 response

`RejectionStatusCode = 429`. `OnRejected`:

- `Retry-After` header from `MetadataName.RetryAfter` when the lease has it.
- JSON body (camelCase like the rest of the API): `{ "code": "rateLimited", "tier": "general|search|auth", "retryAfterSeconds": 23 }`.
- One Serilog `Warning` per rejection with tier and a shortened IP (IPv4 last octet zeroed, IPv6 `/64`): enough to see abuse, no full IP in the log files.

## Frontend

- `lib/api.ts`: `ApiError` gets `retryAfterSeconds` parsed from a 429 body; helper `isRateLimited(error)`.
- `lib/queryClient.ts`: `retry` becomes a function that never retries a 429 (today `retry: 1` would spend another request of the budget).
- A global notice at the top of the main layout, shown while any query/mutation failed with 429 and the user is anonymous: "Zu viele Anfragen. Bitte in {n} s erneut versuchen – angemeldete Nutzer sind nicht begrenzt. [Anmelden]" / English equivalent (`rateLimit.*` keys in both i18n files). Countdown to zero, then the notice offers "Neu laden" which refetches failed queries.
- Auth forms (login, register, magic link, password reset) show the same text through the existing `FormError`, without the login link (they already are the login).
- `Components.md` has no global notice/banner component (only `FormError` for forms). The notice is a new component `RateLimitNotice`, or a `variant` on a generic `Notice` if one is wanted for other banners later; propose in chat before 0015.1f (CLAUDE.md rule).
