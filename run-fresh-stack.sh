#!/usr/bin/env bash
# Starts the fresh test stack defined in compose.fresh.yml (see that file for the ports).
# Extra arguments are passed on to `docker compose up`, e.g. ./run-fresh-stack.sh --force-recreate
set -euo pipefail

cd "$(dirname "$0")"

# Postgres bind mount target (git-ignored via temp/*); created here so it is owned by the current user.
mkdir -p temp/postgres-fresh

FRONTEND_BASE_URL="${FRONTEND_BASE_URL:-http://localhost:5183}" \
  docker compose -p uw-fresh -f compose.yml -f compose.fresh.yml up -d --build "$@"
