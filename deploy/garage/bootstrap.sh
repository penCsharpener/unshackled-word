#!/usr/bin/env bash
# One-shot, idempotent Garage setup: cluster layout, bucket and access key. Prints the values for .env.
# Usage (repo root, garage service running):  deploy/garage/bootstrap.sh
# Prod:  COMPOSE="docker compose --env-file deployment/.env -f compose.yml -f deployment/compose.prod.yml" deploy/garage/bootstrap.sh
set -euo pipefail

COMPOSE=${COMPOSE:-docker compose}
SERVICE=${GARAGE_SERVICE:-unshackledword.garage}
BUCKET=${GARAGE_BUCKET:-unshackledword-media}
KEY_NAME=${GARAGE_KEY_NAME:-unshackledword-api}
CAPACITY=${GARAGE_CAPACITY:-10G}

garage() { $COMPOSE exec -T "$SERVICE" /garage "$@"; }

for _ in $(seq 1 30); do garage status >/dev/null 2>&1 && break; sleep 1; done
garage status >/dev/null

if garage layout show 2>&1 | grep -q "No nodes"; then NEED_LAYOUT=1; else NEED_LAYOUT=0; fi
if [ "$NEED_LAYOUT" = 1 ] || ! garage status 2>&1 | grep -q "dc1"; then
  NODE_ID=$(garage node id -q 2>/dev/null | cut -d@ -f1 | cut -c1-16)
  garage layout assign -z dc1 -c "$CAPACITY" "$NODE_ID"
  CURRENT=$(garage layout show 2>&1 | grep -i "current cluster layout version" | grep -o '[0-9]*' | tail -1 || true)
  garage layout apply --version $(( ${CURRENT:-0} + 1 ))
fi

garage bucket info "$BUCKET" >/dev/null 2>&1 || garage bucket create "$BUCKET"
garage key info "$KEY_NAME" >/dev/null 2>&1 || garage key create "$KEY_NAME" >/dev/null
garage bucket allow --read --write --owner "$BUCKET" --key "$KEY_NAME" >/dev/null

INFO=$(garage key info --show-secret "$KEY_NAME")
ACCESS=$(echo "$INFO" | awk -F': *' '/Key ID/{print $2}')
SECRET=$(echo "$INFO" | awk -F': *' '/Secret key/{print $2}')

cat <<OUT

Add to .env (secrets: keep out of git):
UNSHACKLEDWORD_OBJECT_STORAGE_ACCESS_KEY=$ACCESS
UNSHACKLEDWORD_OBJECT_STORAGE_SECRET_KEY=$SECRET
OBJECT_STORAGE_SERVICE_URL=http://unshackledword.garage:3900
OBJECT_STORAGE_PUBLIC_URL=http://localhost:5173/media
OUT
