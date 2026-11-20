#!/usr/bin/env bash
# Starts the dev stack with the monitoring layer (see compose.monitoring.dev.yml).
# Extra arguments replace the default `up -d`, e.g. ./run-monitoring-dev.sh down  or  ./run-monitoring-dev.sh logs -f unshackledword.monitoring
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

if [ $# -eq 0 ]; then
    set -- up -d
fi

docker compose \
    --env-file .env --env-file .env.monitoring.dev \
    -f compose.yml -f compose.override.yml -f compose.monitoring.yml -f compose.monitoring.dev.yml \
    "$@"
