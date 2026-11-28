# Deploying Unshackled Word in production

This guide covers a single-host, self-hosted deployment with `docker compose` behind a TLS-terminating reverse proxy. The expected scale is small (one VPS or home server is enough).

> **Status of this guide.** The backend (migration, seed, API, MCP server) already has production-capable Dockerfiles and compose services. The repo has **no production frontend image yet**: `src/UnshackledWord.Web/Dockerfile` is a development image that runs the Vite dev server. [Section 5](#5-frontend-production-build) therefore describes a static build served by the reverse proxy; the Dockerfile/Caddyfile/compose snippets in this guide are proposals that are **not part of the repo and not yet verified in a real deployment**. Everything else is derived from `compose.yml`, the Dockerfiles and the API's configuration code.

## 1. Architecture

```
Browser ──HTTPS──▶ Reverse proxy (Caddy/nginx/Traefik)   one public origin, e.g. https://word.example.org
                     ├─ /          static SPA (src/UnshackledWord.Web, `bun run build` → dist/)
                     └─ /api/*  ──▶ unshackledword.tooling.webapi:8080   (ASP.NET Core, FastEndpoints)
                                        └──▶ postgres:18  (private network only)
                                        └──▶ SMTP provider (invites, password reset, magic links)

unshackledword.tooling.db-migration   one-shot, applies numbered SQL scripts, then exits
unshackledword.tooling.mcpserver      optional, MCP over HTTP at /mcp (no authentication, see §10)
```

Serve the SPA and the API from **one origin**. The frontend calls the relative `/api`, the session cookie is `SameSite=Lax`, and the passkey (WebAuthn) relying-party id and origin are derived from `App:FrontendBaseUrl`. A second origin for the API only adds CORS and cookie problems.

## 2. Requirements

| Item | Notes |
|---|---|
| Host | Linux, Docker Engine 24+ with the compose plugin. 2 vCPU / 4 GB RAM is comfortable; the first seed is the heavy part. |
| Disk | The Postgres volume plus the `temp` download cache. Plan for several GB; check `du -sh temp` after your first seed. |
| Domain + TLS | A real hostname with a valid certificate. Passkeys only work on HTTPS (or `localhost`) and are bound to the hostname: changing the domain later invalidates every registered passkey. |
| Outbound internet | The seed downloads STEP Bible data and other sources (cached in `temp/`). |
| SMTP account | Needed for invites, password reset and magic links. The bundled Mailpit only catches mail locally, it never delivers. |
| Git | You deploy from a checkout of this repository (images are built locally, none are published). |

## 3. Prepare the host

```bash
git clone <repo-url> unshackled-word && cd unshackled-word
mkdir -p temp
# The API container runs as UID 1654 (the aspnet image's $APP_UID), the setup jobs run inside it, so the folders below must be writable for it.
# The maintainer page ("Wartung") writes assets/seed and temp/seed-report, so both folders must be writable.
sudo chown -R 1000:1000 assets temp && sudo chmod -R g+w assets temp
```

If you would rather not juggle UIDs, add `user: "1000:1000"` to the `webapi` service.

## 4. Configuration

### 4.1 Compose files: do not run the dev override in production

`compose.override.yml` is git-ignored and **loaded automatically** by `docker compose`. On a developer machine it switches the API to `network_mode: host`, `ASPNETCORE_ENVIRONMENT=Development` (open CORS, OpenAPI), enables mapping editing and disables secure cookies. Never have that file on a production host, or always pass the files explicitly (explicit `-f` disables the auto-load):

```bash
docker compose -f compose.yml -f compose.prod.yml up -d --build
```

### 4.2 `.env`

`.env` is git-ignored. Create it next to `compose.yml` with restrictive permissions (`chmod 600 .env`). Never commit it.

```dotenv
# --- Database ---------------------------------------------------------------
POSTGRES_DB=unshackledword
POSTGRES_USER=unshackledword
POSTGRES_PASSWORD=<long random password>
# Must match the three values above; host is the compose service name.
POSTGRES_CONNECTIONSTRING="Host=unshackledword.tooling.database;Port=5432;Database=unshackledword;Username=unshackledword;Password=<same password>;"

# --- App --------------------------------------------------------------------
ENVIRONMENT=Production
FRONTEND_BASE_URL=https://word.example.org     # public origin, no trailing path. Drives email links, CORS and the passkey origin.
REQUIRE_SECURE_COOKIES=true
REGISTRATION_MODE=Restricted                   # Open | Restricted (see §7)
MAPPING_EDITING_ENABLED=false                  # keep false unless this instance is the one that edits mappings

# --- Email (real SMTP provider) ---------------------------------------------
EMAIL_SMTP_HOST=smtp.example.org
EMAIL_SMTP_PORT=587
EMAIL_SECURITY=StartTls                        # None | StartTls | SslOnConnect | Auto
EMAIL_USERNAME=<smtp user>
EMAIL_PASSWORD=<smtp password>
EMAIL_FROM_ADDRESS=noreply@example.org

MCP_LOG_LEVEL=Information

# --- Monitoring and analytics (only with compose.monitoring.yml, see §9) -----
TELEMETRY_ENABLED=true                         # OpenTelemetry export from API/workers; false = no export
TELEMETRY_OTLP_ENDPOINT=http://unshackledword.monitoring:4317   # dev with the host-network API: http://localhost:4317
TELEMETRY_TRACE_SAMPLE_RATIO=0.1               # share of traces kept
GRAFANA_ADMIN_PASSWORD=<long random password>  # required
GRAFANA_ROOT_URL=https://monitor.example.org
GRAFANA_SMTP_STARTTLS_POLICY=OpportunisticStartTLS   # Grafana SMTP uses the EMAIL_* values above
MONITORING_ALERT_EMAIL=ops@example.org         # alert recipient
MONITORING_GRAFANA_DB_PASSWORD=<random>        # login password of the Postgres role uw_grafana (§9.2)
MONITORING_EXPORTER_DB_PASSWORD=<random>       # login password of the Postgres role uw_monitor (§9.2)
UMAMI_DB_PASSWORD=<random>                     # Postgres role umami
UMAMI_APP_SECRET=<long random string>          # required
# Frontend build time (§5): set these before `bun run build`
VITE_UMAMI_WEBSITE_ID=<id from Umami, §9.6>
VITE_OPERATOR_NAME=<name for the privacy page>
VITE_OPERATOR_CONTACT=<contact for the privacy page>
```

Notes:

* The compose file maps these to `UNSHACKLEDWORD_*` environment variables (double underscore = nesting, e.g. `UNSHACKLEDWORD_EMAIL__SMTPHOST`). Any setting in `appsettings.json` can be overridden the same way.
* `ENVIRONMENT` becomes `UNSHACKLEDWORD_ENVIRONMENT`, which is an ordinary config key. What switches ASP.NET Core's hosting environment is `ASPNETCORE_ENVIRONMENT`; when it is unset the app runs as **Production**, which is what you want (HTTPS redirection and the strict single-origin CORS policy from `App:FrontendBaseUrl` only apply in Production). Make sure it is not set to `Development`.
* The default `POSTGRES_PASSWORD=test` in `compose.yml` is for local use only. The README's quick-start `.env` (`Password=test`, `ENVIRONMENT=Development`) must not be used in production.
* Registration `AllowedEmailDomains` / `AllowedEmails` are lists; set them with indexed variables, e.g. `UNSHACKLEDWORD_REGISTRATION__ALLOWEDEMAILDOMAINS__0=example.org`.

### 4.3 `compose.prod.yml` (proposal)

Layered on `compose.yml`. It closes the ports that must never be public, removes the dev-only services and adds restart policies, the proxy and the frontend build.

```yaml
services:
  unshackledword.tooling.database:
    container_name: unshackledword-db
    restart: unless-stopped
    ports: !reset []                 # no host port; only the compose network reaches Postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 3s
      retries: 10

  unshackledword.tooling.db-migration:
    restart: "no"
    depends_on:
      unshackledword.tooling.database:
        condition: service_healthy

  unshackledword.tooling.webapi:
    restart: unless-stopped
    ports: !reset []                 # the proxy reaches it as unshackledword.tooling.webapi:8080
    depends_on:
      unshackledword.tooling.database:
        condition: service_healthy
      unshackledword.tooling.db-migration:
        condition: service_completed_successfully
      mailpit: !reset null
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_FORWARDEDHEADERS_ENABLED=true   # trust X-Forwarded-Proto/-For from the proxy
    volumes:
      - dataprotection:/home/app/.aspnet           # see §6: keeps sessions valid across restarts

  mailpit:
    profiles: ["dev"]                # never started in production
    ports: !reset []

  unshackledword.tooling.mcpserver:
    profiles: ["mcp"]                # opt in with `--profile mcp`, see §10
    ports: !reset []

  unshackledword.web:
    profiles: ["dev"]                # the Vite dev image is not for production

  proxy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    depends_on:
      - unshackledword.tooling.webapi
    volumes:
      - ./deploy/Caddyfile:/etc/caddy/Caddyfile:ro
      - ./src/UnshackledWord.Web/dist:/srv/web:ro   # output of §5
      - caddy_data:/data
      - caddy_config:/config

volumes:
  dataprotection:
  caddy_data:
  caddy_config:
```

`deploy/Caddyfile` (Caddy obtains and renews the certificate automatically):

```caddyfile
word.example.org {
    encode zstd gzip

    handle /api/* {
        reverse_proxy unshackledword.tooling.webapi:8080
    }

    handle {
        root * /srv/web
        try_files {path} /index.html      # SPA fallback for client-side routes
        file_server
    }
}
```

For nginx or Traefik the same two rules apply: `/api/*` to the API, everything else to the static files with an `index.html` fallback, and forward `Host`, `X-Forwarded-For` and `X-Forwarded-Proto`.

## 5. Frontend production build

The frontend is a static SPA. Build it inside Docker (project rule: no host `node_modules`). The API URL is not baked in: the app uses the relative `/api`, which the proxy routes.

```bash
docker run --rm -v "$PWD/src/UnshackledWord.Web:/app" -w /app oven/bun:1.4 \
  sh -c "bun install --frozen-lockfile && bun run build"
# result: src/UnshackledWord.Web/dist/
```

`bun run build` runs `tsc --noEmit` first, so a type error fails the build. Before releasing, run the full gate once: `docker compose run --rm --no-deps unshackledword.web bun run check`.

Build-time variables: `VITE_API_URL` defaults to `/api` (leave it unset for the single-origin setup) and `VITE_WORD_DETAIL_SOURCE` uses the real API unless it is set to `mock`, so no extra variables are needed. They are baked into the bundle at build time; changing them means rebuilding.

A dedicated multi-stage image (bun build stage → `caddy`/`nginx` runtime stage with `dist/`) is the cleaner long-term solution; it would replace the bind mount of `dist/` and remove the build step from the host.

## 6. First deployment

```bash
# 1. build the frontend (§5), then:
docker compose -f compose.yml -f compose.prod.yml build

# 2. start the database and wait until healthy
docker compose -f compose.yml -f compose.prod.yml up -d unshackledword.tooling.database

# 3. schema (idempotent; prints "No new scripts" when up to date)
docker compose -f compose.yml -f compose.prod.yml run --rm unshackledword.tooling.db-migration

# 4. the rest (there is no seed container; the data is imported from the web UI, see below)
docker compose -f compose.yml -f compose.prod.yml up -d
```

**First start: register, then "Ersteinrichtung".** The database is empty after the migration and the reader shows "Noch keine Bibeltexte importiert.". Open the site and register the first account: it becomes the Admin (if registration mail does not work yet, see §7 for the SMTP settings). Open the gear menu in the header, entry "Ersteinrichtung" / "Initial setup" (`/edit/setup`, Admin only). The page lists the 22 seed steps in four groups (Grundtext Elb1871, STEP Bible, Weitere Quellen, Abgeleitete Daten) with a status dot and the table counts per step. Press "Alle ausführen" / "Run all" (or "Gruppe ausführen", or the button of one row); steps run in catalog order, only red steps are taken, a step waits for its prerequisites ("braucht: …"), the run stops at the first failure, and the log under the page shows progress. A failed step that wrote nothing stays red and can simply run again. Everything runs as a background job inside the API process (one at a time, 409 while another seed job runs); closing the browser does not stop it, restarting the API does. Measured on a developer machine from an empty database with fresh downloads: about 5.5 minutes for all 22 steps (the STEP group about 90 s, the materialized views about a minute); slower connections take longer. The API container needs outbound internet for the STEP, SR-GNT, SBL and Swete downloads (cached in `temp/`) and the repo `assets/` folder (it is part of the API image). Each seed job has its own command timeout (`Seeding:CommandTimeoutSeconds`, default 900).

What the services do:

* **db-migration** applies the numbered raw-SQL scripts in `src/UnshackledWord.Tooling.Postgres.Migration/scripts/` and records them. Migrations only create schema; all seed data is imported afterwards, from the setup page.
* **The setup page** (library `UnshackledWord.Seeding`, `SeedCatalog`) downloads the open-licence sources into `temp/` (cached, later runs are faster) and imports them, then imports the repo seed (`assets/seed`, `assets/Elb1871Mappings`: German morphology/lemmas, word mappings, STTS tags, revision). The setup is meant for the **initial start**; afterwards only the mapping seed is updated, via the maintainer page (§8). Steps that are already green are not run again (only the materialized views, "Ansicht neu berechnen", may be refreshed), so a second "Alle ausführen" is rejected. STEP Bible data is downloaded on the fly and never redistributed; if an upstream URL fails, the step fails, so read the log and the data-quality page afterwards. Some steps take their data from the maintainer's own tables (`Elb1871Morphology`, `TskCurations`, `LxxWordTags`, `RevisionVerses`): the repo seed may hold fewer rows than the maintainer's database, the step then shows green with "nichts zu importieren".
* **SeedDb CLI as an alternative.** The same catalog can run from a shell against any database: `dotnet run --project src/UnshackledWord.Tooling.SeedDb` (all steps in catalog order, in one process), with the connection string and `AppSettings:DatabaseSeeding:SolutionAssetsPath`/`SolutionTempPath` as described in the README. Use it for scripted or headless installs; it is no compose service any more, and the verbs `export-seed`, `anchor-check` and `rebase` stay CLI tools for the maintainer. Do not run it against a database that already holds live edits.
* A bare `docker compose up -d` only starts the migration (a no-op when nothing is new), the API and the database; nothing seeds on start.

### Session keys

The API uses ASP.NET cookie authentication, whose cookies are encrypted with Data Protection keys. The code does not configure key persistence, so in a container the keys live in `/home/app/.aspnet/DataProtection-Keys` and are lost with the container: **every deploy would log everyone out**. The `dataprotection` volume in `compose.prod.yml` fixes this without a code change. If the volume is created root-owned, make it writable for UID 1654 once:

```bash
docker compose -f compose.yml -f compose.prod.yml run --rm --user root --entrypoint sh unshackledword.tooling.webapi \
  -c "mkdir -p /home/app/.aspnet && chown -R 1654:1654 /home/app/.aspnet"
```

### Verify

```bash
docker compose -f compose.yml -f compose.prod.yml ps
curl -sI https://word.example.org/                              # 200, SPA
curl -s  https://word.example.org/api/auth/me -o /dev/null -w "%{http_code}\n"   # 401 when anonymous: API reachable, auth active
docker compose -f compose.yml -f compose.prod.yml logs --tail=100 unshackledword.tooling.webapi
```

(Use any public read endpoint from `api/unshackled-word/` (Bruno collection) if `/api/auth/me` differs in your build.)

## 7. Accounts, first admin, registration mode

* Reading is public; writing requires login. Endpoints are anonymous unless marked `[RequiresLogin]`.
* **The first account that registers automatically becomes admin** (and user). So right after the first deployment, register your own account before anyone else can, while `REGISTRATION_MODE=Open` is acceptable, or temporarily keep the site unreachable until you have registered.
* Afterwards set `REGISTRATION_MODE=Restricted`: an address may then only register if it matches `Registration:AllowedEmailDomains` / `AllowedEmails` or carries an invite code. Invites are sent by email, so SMTP must work. Restart the API after changing the mode.
* Sign-in methods are password, magic link/email token and passkeys (Fido2). Passkeys require `FRONTEND_BASE_URL` to be the exact public HTTPS origin.
* Sessions: cookie `unshackled_session`, HttpOnly, `SameSite=Lax`, `Secure` when `REQUIRE_SECURE_COOKIES=true`, 14 days sliding, invalidated by a password reset.
* Test email delivery by requesting a password reset for your own account. If nothing arrives, check the API log for MailKit errors (`EMAIL_SECURITY` must match the port: 587 → `StartTls`, 465 → `SslOnConnect`).

## 8. Operating

### Updates

```bash
git pull
docker compose -f compose.yml -f compose.prod.yml build
# rebuild the frontend if src/UnshackledWord.Web changed (§5)
docker compose -f compose.yml -f compose.prod.yml run --rm unshackledword.tooling.db-migration
docker compose -f compose.yml -f compose.prod.yml up -d unshackledword.tooling.webapi proxy
```

Schema changes ship as new numbered scripts; never edit an applied script. Take a backup before every update that adds migrations.

### Mapping and revision seed updates

The official repo ships a rolling seed for mappings and the Elb1871 revision. After `git pull`, open the maintainer page (menu "Wartung" / "Maintenance", section "Seed-Rundlauf", admin/maintainer only):

* **Import** fills only empty tables.
* **Update** merges newer seed data and keeps locally changed variants/verses (it lists them).
* **Export** writes your own data to `assets/seed`; **round-trip check** verifies the export byte for byte.
* **Rebase** (after parser or base-text fixes) empties reader tables while it runs: do it when nobody is reading, and only after a database backup.

These are background jobs inside the API process; their state is lost if the API restarts. Self-hosters who keep their own data simply never import the repo seed over it.

### TSK curation seed

`assets/seed/tsk/curations.csv` is the change file of the curated TSK cross references (highlight/hide, comments, word links, added links; no user data). The setup step `tsk-curation` applies it only to an empty `TskCurations`; later it moves only via the maintainer menus ("Wartung": own export button, also written by the "Alles" seed export/round trip and zip; "Importe" import, option "Exakt wie Datei" to also delete). Migrations 0044–0046 (stored TSK keys, curations and audit, proposals) must run before the API reports healthy; back up before updating.

### Vocab seed

`assets/seed/vocab` (`entries.csv`, `keys.csv`, `meanings.csv`) holds the vocab lexicon content: entries, Strong's keys, German meanings (derived/manual, accepted, pinned, ranks), dictionary forms and the audio object *keys*. It never holds progress (cards, sessions, review log, settings, change rows) and no ids. The setup page does not import it; the maintainer menu "Importe" does (`/edit/imports`); "Import" and "Update" on the maintainer page run the same sync: entries are matched by `(Track, Lemma)` (fallback: a Strong's key they hold), matched entries take the seed, unknown ones are created, entries missing from the seed are left alone. A learning-relevant difference (accepted meanings, dictionary form, keys, lemma) bumps the entry's content version and writes a change row, so learners see it in their change list; anything else is updated silently. The audio key is only set from the seed, never cleared by it. Export fails if `(Track, Lemma)` exists twice among live entries: merge or retire the duplicate first.

### LXX (roadmap 0028)

The setup step `lxx-text` (or the SeedDb CLI) downloads the Swete files (about 12 MB, pinned commit `AppSettings:DatabaseSeeding:Swete:Commit`) into `temp/SeedData/Swete/` on first run and imports the text only into an empty `LxxVerses`. The confirmed LXX tags in `assets/seed/lxx/` are applied only while no tag is confirmed; afterwards use the maintainer export ("Wartung") and import ("Importe"). A STEP re-import renumbers the Hebrew words: SeedDb then deletes the LXX Hebrew links and re-imports them from `assets/seed/lxx/links.csv`, so export the LXX tags before it.

AI tagging runs (mode `lxx-tag`) need a model profile under `AgentMapping:Profiles` and cost money, so estimate first. Set `MaxOutputTokens` to at least 16384, or `MaxVersesPerPackage` to about 10, for this mode.

### Backups

Back up Postgres, plus the seed folders if you export from this instance.

```bash
# nightly logical backup (cron on the host)
docker compose -f compose.yml -f compose.prod.yml exec -T unshackledword.tooling.database \
  pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" > /backups/unshackledword-$(date +%F).dump

# restore into an empty database
docker compose -f compose.yml -f compose.prod.yml exec -T unshackledword.tooling.database \
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists < /backups/unshackledword-YYYY-MM-DD.dump
```

The Umami database (`umami`) is **not** backed up on purpose: losing the analytics history is acceptable. The `pg_dump` above only covers the app database. Grafana, Loki, Tempo, Prometheus and Uptime Kuma data live in `temp/monitoring` and are not backed up either; the dashboards and alert rules are provisioned from files in `deploy/monitoring/` (git).

Also keep: `.env` (in a password manager, not in git), the `dataprotection` volume (optional, only preserves sessions) and `assets/seed` (it is in git once you commit exports). `temp/` is a download cache and can be rebuilt. Test a restore at least once. Do not back up by copying the live Postgres data directory.

### Notes backup and restore

Notes (roadmap 0023) live in the app database, so the `pg_dump` above already covers them. The file export is a second, portable copy that does not depend on the database: it survives a fresh database and can move notes to another instance. `temp/notes/` is the one folder under `temp/` that is **not** a rebuildable cache.

**Export.** On `/edit/imports` (maintainer menu "Importe"), card "Notizen", press the export button. The same via API: `POST api/maintainer/notes/export` (a background job, 202; poll `GET api/maintainer/seed/status`). It writes every user's notes to `temp/notes/notes-YYYYMMDD-HHmmss.json` on the host, because compose mounts `./temp` into the API container. Back up that folder with the same schedule as the database dump, e.g.:

```bash
rsync -a temp/notes/ /backups/notes/
```

**Restore.** On a fresh database (schema migrations as in §6; the setup page is not needed for notes):

1. Users register again with the same e-mail address. Import matches authors by normalised e-mail, so a note whose author does not exist yet is skipped and listed in the report.
2. Put the file into `temp/notes/` (or keep it there from the backup) and open `/edit/imports`, card "Notizen": choose the file (newest preselected) and press import. The same via API: `POST api/maintainer/notes/import` with `{ "fileName": "notes-….json" }`.

Import rules: a missing note is inserted; an existing note is updated only if the file's `updatedAt` is newer, so re-running the same file changes nothing. Approver unknown: the approval is kept with no approver. Nothing is deleted: a note that is missing from the file stays. On a database that already holds the notes, a re-import does not carry later maintainer approve/hide actions (they do not change `updatedAt`); a fresh database gets everything. Check the report (inserted, updated, unchanged, skipped with reasons) after each run.

### Object storage (Garage, optional, vocab audio)

Only needed for vocab audio. Without `ObjectStorage` config the API does not register storage and `api/health/ready` stays green; with it, a `storage` check (bucket reachable) is added.

Step by step (dev: plain `docker compose`; prod: add `--env-file deployment/.env -f compose.yml -f deployment/compose.prod.yml` to every `docker compose` call and run `docker compose` the same way in `COMPOSE=...` below):

1. Generate the Garage secrets yourself and put them into `.env` (never in git). Garage rejects placeholders and all zeros; the RPC secret must be exactly 64 hex characters:

```bash
openssl rand -hex 32   # -> GARAGE_RPC_SECRET
openssl rand -hex 24   # -> GARAGE_ADMIN_TOKEN
```

2. Create the data directories (bind mounts `deploy/garage/volumes/meta` and `/data`, overridable with `GARAGE_META_DIR`/`GARAGE_DATA_DIR`; data from the old named volumes `garage_meta`/`garage_data` must be copied there once) and start Garage (single node, `dxflrs/garage:v2.4.1`, config `deploy/garage/garage.toml`):

```bash
mkdir -p deploy/garage/volumes/meta deploy/garage/volumes/data
docker compose up -d unshackledword.garage
```

3. Set up the cluster once: capacity, bucket and access key (a one-node cluster; the capacity is the disk space Garage may use, adjust `10G`). `G` runs the Garage CLI in the container; in prod use the long compose command from the note above instead of `docker compose`:

```bash
G="docker compose exec -T unshackledword.garage /garage"

# capacity: assign this node to zone dc1 with 10G, then apply the layout (version 1 on a fresh cluster)
NODE_ID=$($G node id -q | cut -d@ -f1 | cut -c1-16)
$G layout assign -z dc1 -c 10G "$NODE_ID"
$G layout show                       # review the staged change
$G layout apply --version 1

# bucket and key
$G bucket create unshackledword-media
$G key create unshackledword-api
$G bucket allow --read --write --owner unshackledword-media --key unshackledword-api

# the values for step 4 ("Key ID" = access key, "Secret key" = secret key)
$G key info --show-secret unshackledword-api
```

Changing the capacity later: `$G layout assign -z dc1 -c 20G "$NODE_ID"`, then `$G layout apply --version <current version + 1>` (`$G layout show` prints it).

4. Put the values from `key info` into `.env` and set the public URL to your own origin plus `/media`:

```bash
GARAGE_RPC_SECRET=<openssl rand -hex 32>
GARAGE_ADMIN_TOKEN=<openssl rand -hex 24>
OBJECT_STORAGE_SERVICE_URL=http://unshackledword.garage:3900
OBJECT_STORAGE_PUBLIC_URL=https://example.org/media      # dev: http://localhost:5173/media
UNSHACKLEDWORD_OBJECT_STORAGE_ACCESS_KEY=GK...
UNSHACKLEDWORD_OBJECT_STORAGE_SECRET_KEY=...
```

5. Recreate the API: `docker compose up -d unshackledword.tooling.webapi`.

Reads are presigned GET URLs on `OBJECT_STORAGE_PUBLIC_URL`. The signature covers the public host, so the proxy in front must strip only `/media` and forward the `Host` header unchanged (`nginx.prod.conf` uses `$http_host`; Nginx Proxy Manager must keep the Host header too). `ObjectStorage:Region` and `s3_region` in `garage.toml` must both be `garage`; a mismatch is answered with a bare 400. Uploads go through the API, so Garage's S3 port is not published in production.

The vocab seed stores only the audio *keys*; the audio objects live in Garage and must be backed up separately (neither the Postgres dump nor `assets/seed` contains them). Backup: copy the Garage directories next to the Postgres dump, e.g. with the container stopped or after `garage meta snapshot`:

```bash
docker run --rm -v "$PWD/deploy/garage/volumes/data:/data:ro" -v /backups:/backups rclone/rclone \
  copy /data /backups/garage-$(date +%F)/data
docker run --rm -v "$PWD/deploy/garage/volumes/meta:/meta:ro" -v /backups:/backups rclone/rclone copy /meta /backups/garage-$(date +%F)/meta
```

Restoring means copying both folders back into empty directories before the first start. Without audio nothing is lost if Garage is gone: the audio is re-recorded.

### Logs and health

* Serilog writes to the console (use `docker compose logs`) and to `./bin/unshackled-word.tooling.web-api-<date>.log` inside the container (ephemeral). Set levels with `UNSHACKLEDWORD_SERILOG__MINIMUMLEVEL__DEFAULT=Warning` (and `...__OVERRIDE__MICROSOFT`) if the logs are too chatty. Add `logging: { driver: json-file, options: { max-size: "10m", max-file: "5" } }` per service to cap Docker log growth.
* Health endpoints (anonymous, exempt from rate limiting, not traced):
  * API: `GET /api/health/live` always returns 200 while the process runs. `GET /api/health/ready` returns 503 when the database is down or a migration is pending (checks `database` and `migrations`).
  * MCP server: `/health/live` and `/health/ready` with the same meaning.
  * The aspnet image has no `curl`, so the compose files define no healthcheck for these services. Uptime Kuma checks from outside (§9.5). Use `/api/health/ready` for uptime monitoring.
* Postgres 18 stores data under `/var/lib/postgresql` (the volume mount in `compose.yml`). When upgrading to a new Postgres major version, dump and restore; do not just change the image tag.

## 9. Monitoring and analytics (optional)

Roadmap 0021. A second compose layer, `compose.monitoring.yml`, adds Grafana/Loki/Tempo/Prometheus (one `grafana/otel-lgtm` container), a Postgres exporter, Uptime Kuma and Umami (privacy-friendly web analytics, no cookies). All of it binds to `127.0.0.1` only; the public reaches it through the nginx of `deployment/nginx.prod.conf` (§9.3).

```bash
docker compose -f compose.yml -f compose.monitoring.yml -f compose.prod.yml up -d
```

Use the same three `-f` files for every later command in production (§6, §8), otherwise compose treats the monitoring services as orphans.

### 9.1 RAM

`otel-lgtm` needs roughly 1 to 1.5 GB RAM on top of the app. On a 2 GB host run only Uptime Kuma and Umami (`docker compose -f compose.yml -f compose.monitoring.yml -f compose.prod.yml up -d uptime-kuma umami`) and set `TELEMETRY_ENABLED=false`. The data folder `temp/monitoring/lgtm` must be writable by the container user (`chmod -R a+rwX temp/monitoring` once, or `chown` it to the UID the image reports).

### 9.2 One-time database steps

Migration `0036_Monitoring.sql` creates the roles `uw_monitor` (`pg_monitor`, for the exporter) and `uw_grafana` (SELECT on the views of schema `monitoring` only) as `NOLOGIN`. Give them logins once, with the passwords from `.env`:

```sql
ALTER ROLE uw_monitor LOGIN PASSWORD '<MONITORING_EXPORTER_DB_PASSWORD>';
ALTER ROLE uw_grafana LOGIN PASSWORD '<MONITORING_GRAFANA_DB_PASSWORD>';
CREATE ROLE umami LOGIN PASSWORD '<UMAMI_DB_PASSWORD>';
CREATE DATABASE umami OWNER umami;
```

```bash
docker compose -f compose.yml -f compose.prod.yml exec unshackledword.tooling.database psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
```

The layer adds `shared_preload_libraries=pg_stat_statements` to Postgres, which needs **one restart of the database container** the first time (`up -d` with the new layer recreates it; plan a short downtime).

### 9.3 Subdomains and nginx

Create three DNS names and NPM proxy hosts that all forward to the same address as the main site (`WEB_BIND`, nginx in `unshackledword-web`), with their own certificates:

| Host | Target (inside the compose network) | Purpose |
|---|---|---|
| `monitor.<domain>` | `unshackledword.monitoring:3000` | Grafana |
| `status.<domain>` | `uptime-kuma:3001` (websocket) | Uptime Kuma |
| `analytics.<domain>` | `umami:3000` | Umami UI |
| `<domain>/stats/script.js`, `/stats/api/send` | `umami:3000`, `/stats` prefix stripped | tracker, same origin |

`deployment/nginx.prod.conf` already contains these server blocks (replace `example.org`). Key parts: upstreams are set through variables with `resolver 127.0.0.11`, so nginx starts even when the monitoring layer is down, Uptime Kuma gets the websocket upgrade headers, and the tracker location only allows the two public paths:

```nginx
location ~ ^/stats/(script\.js|api/send)$ {
    set $umami http://umami:3000;
    rewrite ^/stats/(.*)$ /$1 break;     # /stats/script.js -> /script.js, /stats/api/send -> /api/send
    proxy_pass $umami;
}
```

`compose.prod.yml` needs no change: the monitoring services share the default compose network with `unshackledword.web-prod`. After editing the config: `docker compose -f compose.yml -f compose.monitoring.yml -f compose.prod.yml exec unshackledword.web-prod nginx -s reload`.

### 9.4 Grafana

* Log in at `https://monitor.<domain>` as `admin` with `GRAFANA_ADMIN_PASSWORD` (required; compose refuses to start without it). Anonymous access and sign-up are off.
* The dashboards "API", "Postgres" and "Mapping & Agents" and four alert rules are provisioned from files (`deploy/monitoring/`) and survive restarts. Do not edit them in the UI; change the files.
* Alert mail goes to `MONITORING_ALERT_EMAIL` through the same SMTP account as the login links (`EMAIL_*`). A flapping alert can exhaust the send limit of that account and delay login mails; use a dedicated sending account if your provider has a low limit.
* Retention: Loki and Tempo 14 days, Prometheus 30 days (configs in `deploy/monitoring/`).
* Telemetry export from the API is controlled by `TELEMETRY_ENABLED`, `TELEMETRY_OTLP_ENDPOINT` and `TELEMETRY_TRACE_SAMPLE_RATIO`.

### 9.5 Uptime Kuma

Open `https://status.<domain>`, create the admin account on first visit, then:

* Monitor 1: HTTP(s), `https://<site>/api/health/ready`, interval 60 s (catches database down and pending migrations).
* Monitor 2: HTTP(s), `https://<site>/` (the SPA), interval 60 s.
* Notification: SMTP with the values of `EMAIL_*` (host, port, security, user, password, from), recipient `MONITORING_ALERT_EMAIL`; attach it to both monitors.
* Development: notification SMTP host `mailpit`, port 1025, no TLS. The monitor URL for the host-network dev API is `http://host.docker.internal:5142/api/health/ready`.

### 9.6 Umami

1. Open `https://analytics.<domain>`, log in with the default account and change the password immediately.
2. Settings, Websites, add a website named "Unshackled Word" with your domain, and copy its website id.
3. Put the id into `VITE_UMAMI_WEBSITE_ID` (plus `VITE_OPERATOR_NAME` and `VITE_OPERATOR_CONTACT` for the privacy page `/datenschutz` / `/privacy`) and rebuild the frontend (§5). The tracker script is loaded from `/stats/script.js` on your own origin.

### 9.7 Verify

```bash
ss -tlnp    # besides 80/443 (and 22) every listener of the stack must be on 127.0.0.1: 3000, 3001, 3002, 4317, 4318
curl -s https://<site>/api/health/ready                      # 200
curl -sI https://<site>/stats/script.js                      # 200 once Umami runs
curl -sI https://<site>/stats/api/heartbeat                  # must NOT reach Umami (404 from the SPA fallback or nginx)
```

## 10. Security checklist

- [ ] Strong, unique `POSTGRES_PASSWORD`; Postgres has **no published host port** (the default `compose.yml` publishes `5432` on all interfaces, which `compose.prod.yml` resets).
- [ ] Only ports 80/443 of the proxy are reachable from outside (firewall: allow 22, 80, 443; deny the rest).
- [ ] `compose.override.yml` is absent; `ASPNETCORE_ENVIRONMENT` is not `Development`; Swagger/OpenAPI is therefore off.
- [ ] `REQUIRE_SECURE_COOKIES=true`, `FRONTEND_BASE_URL` is the exact `https://` origin.
- [ ] `REGISTRATION_MODE=Restricted` after the first admin exists.
- [ ] `MAPPING_EDITING_ENABLED=false` on read-mostly instances.
- [ ] Mailpit is not running (its web UI would expose all outgoing email, including login links).
- [ ] **MCP server:** it speaks stateless HTTP at `/mcp` and has **no authentication** (access policy is still undecided, see `CLAUDE.md`). Do not expose it publicly: leave it off (profile `mcp`), or bind it to localhost / a VPN and, if the proxy exposes it, protect that route with basic auth or an IP allowlist.
- [ ] `.env` is `chmod 600`, not in git; SMTP credentials are a dedicated sending account.
- [ ] Host OS and Docker images are updated regularly (`docker compose pull` for `postgres`/`caddy`, rebuild for the .NET images to pick up base-image patches).
- [ ] Monitoring layer: `GRAFANA_ADMIN_PASSWORD` is strong, Grafana anonymous access and sign-up are off, and `ss -tlnp` shows only `127.0.0.1` bindings for 3000/3001/3002/4317/4318 besides 80/443 (§9.7).
- [ ] The pinned monitoring images (`otel-lgtm`, `postgres-exporter`, `uptime-kuma`, `umami`) are bumped deliberately together with the other images; the roles `uw_grafana` (views of schema `monitoring` only) and `uw_monitor` have only the grants from migration 0036.
- [ ] Backups run, are stored off the host, and a restore has been tested.

## 11. Troubleshooting

| Symptom | Likely cause |
|---|---|
| Everyone logged out after each deploy | Data Protection keys not persisted: add the `dataprotection` volume (§6). |
| Login works but the next request is anonymous | `REQUIRE_SECURE_COOKIES=true` while the browser reaches the app over plain HTTP, or the proxy is not sending `X-Forwarded-Proto`. Use HTTPS end to end. |
| Passkey registration fails with an origin/RP error | `FRONTEND_BASE_URL` differs from the URL in the address bar (scheme, host, port). Restart the API after changing it. |
| Browser CORS errors on `/api` | The SPA is served from a different origin than `FRONTEND_BASE_URL`, or the proxy does not route `/api` on the same origin. |
| Emails (invites, reset) never arrive | Still pointing at `mailpit`; wrong `EMAIL_SECURITY` for the port; provider blocks `FROM` address. Check the API log. |
| Maintainer page says assets are not reachable / export fails | `assets` or `temp` not writable for the container user (§3), or not mounted. |
| `db-migration` cannot connect | Wrong `POSTGRES_CONNECTIONSTRING` host (must be the service name `unshackledword.tooling.database`) or the database is not healthy yet. |
| Data-quality page returns 500 after an update | A migration is pending: run the `db-migration` service. |
| A setup step fails (download) | An upstream source is unreachable or changed; read the log on `/edit/setup` and the `SeedImports` rows on the data-quality page; a step that wrote nothing can run again, otherwise read the README "Seed" section first. |
| Deep link such as `/read/...` returns 404 | The proxy lacks the SPA fallback to `index.html`. |

## 12. Open items (not solved in the repo yet)

* A production frontend image (multi-stage build, static server) and a committed `compose.prod.yml` / `deploy/Caddyfile`.
* Data Protection key persistence configured in code instead of via a volume.
* A compose healthcheck for the API needs a probe binary in the image (the aspnet image has no `curl`); deferred, Uptime Kuma checks `/api/health/ready` from outside (§8, §9.5).
* Frontend errors are not captured (GlitchTip or similar is deferred).
* An access policy (authentication) for the MCP server.
* Published container images, so a deployment does not need to build from source.
