# 0022 · Garage and audio

## Garage

Port Lexio roadmap 0048 (`../lexio/docs/Roadmaps/0048 Garage replaces MinIO.md`, `../lexio/garage/`). There is no MinIO migration here: this is a fresh store.

- Compose service `unshackledword.garage` (`dxflrs/garage:v2.4.1`, the version Lexio verified), data and meta volumes, `deploy/garage/garage.toml` with `s3_region = "garage"`. A one-shot bootstrap script (`deploy/garage/bootstrap.sh`) assigns the layout, creates the bucket `unshackledword-media` and a key, and prints the key for `.env`. Idempotent.
- `ObjectStorageOptions` (`ObjectStorage:ServiceUrl`, `PublicServiceUrl`, `Region`, `Bucket`, `AccessKey`, `SecretKey`) with FluentValidation. Env vars use `UNSHACKLEDWORD_OBJECTSTORAGE__*`.
- `Application/Abstractions/IObjectStorage` (`PutAsync`, `DeleteAsync`, `GetReadUrl`) and `Infrastructure/ObjectStorage/S3ObjectStorage` over `AWSSDK.S3` with an explicit `AuthenticationRegion`. Lexio's lesson: Garage answers a region mismatch with a bare `400`.
- Reads use a presigned GET against `PublicServiceUrl`. The web nginx (prod) and Vite (dev) proxy `/media/` to Garage byte-for-byte, so SigV4 survives the hop. Uploads go through the API (small files, one maintainer), so there is no presigned PUT.
- `api/health/ready` gets a `storage` check (bucket HEAD), but only when `ObjectStorage` is configured. A self-hoster without audio keeps a green ready check.
- Backups: `deployment.md` gets a section that copies the Garage volume with `rclone` next to the Postgres dump.

## Audio

- Object key `vocab/{track}/{entryId}-{shortHash}.mp3`. A new upload gets a new key, so browser caches never serve the old file. The old object is deleted after the row is updated.
- `POST api/maintainer/vocab/entries/{id}/audio` (multipart, `audio/mpeg`, ≤ 1 MB, MP3 frame sync or `ID3` magic checked on the server) and `DELETE`.
- Recording in the editor ports Lexio 0020/0029: microphone → PCM → mp3 in the browser with `@breezystack/lamejs`, no `MediaRecorder`, so iOS Safari can play it. This is the only new frontend package for audio and it needs approval in chat.
- Pronunciation tradition (Erasmian/reconstructed Koine; Sephardi/Israeli Hebrew) is the maintainer's choice when recording. It is noted on the vocab page in one line, not modelled.
