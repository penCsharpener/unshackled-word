# 0015 · Configuration

## appsettings.json (defaults shipped in the repo)

```json
"RateLimiting": {
  "Enabled": false,
  "General": { "PermitLimit": 300, "WindowSeconds": 60, "SegmentsPerWindow": 6 },
  "Strict":  { "PermitLimit": 20,  "WindowSeconds": 60, "SegmentsPerWindow": 6 }
},
"ReverseProxy": {
  "KnownProxies": [],
  "KnownNetworks": [],
  "ForwardLimit": 1
}
```

- `QueueLimit` is fixed at 0 (reject at once, no waiting requests holding threads); not configurable.
- General 300/min: one chapter view fires chapter text, originals, cross references and a request per hover card; 300 leaves room for fast readers. Strict 20/min per group: a human searches far less; 20 login attempts per minute per IP is still too many for brute force over time, which is why `auth` may get its own numbers later (see [06-risks.md](06-risks.md)).
- Options class `RateLimitingOptions` with FluentValidation-style checks at startup (`ValidateOnStart`): `PermitLimit >= 1`, `WindowSeconds` 1–3600, `SegmentsPerWindow` 1–`WindowSeconds`. A bad value fails startup, not the first request.
- `ReverseProxy` entries: IPs (`10.0.0.5`) and CIDR networks (`172.16.0.0/12` for a proxy on a Docker network whose container IP changes). Empty lists → `UseForwardedHeaders` is not registered and `X-Forwarded-For` is ignored.
- Startup log `Warning` when `RateLimiting:Enabled` is true in Production and both proxy lists are empty: every visitor would share the proxy's IP and one bucket.

## Environment variables (Docker)

Prefix `UNSHACKLEDWORD_`, double underscore for nesting, arrays by index:

```
UNSHACKLEDWORD_RATELIMITING__ENABLED=true
UNSHACKLEDWORD_RATELIMITING__GENERAL__PERMITLIMIT=300
UNSHACKLEDWORD_RATELIMITING__STRICT__PERMITLIMIT=20
UNSHACKLEDWORD_REVERSEPROXY__KNOWNNETWORKS__0=172.16.0.0/12
```

`compose.yml` passes `RATE_LIMITING_ENABLED` (default `false`) and `REVERSE_PROXY_NETWORK` from `.env` to the WebApi service; README's setup section and `.env` example list both.
