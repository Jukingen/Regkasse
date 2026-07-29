#!/usr/bin/env bash
# Apply EF Core migrations for a deploy stage (CI).
#
# Prefer a host webhook (MIGRATE_WEBHOOK_URL). Fallback: DATABASE_CONNECTION + dotnet ef.
#
# Env:
#   STAGE                 staging|canary|production
#   MIGRATE_WEBHOOK_URL   optional POST target
#   DATABASE_CONNECTION   optional Npgsql connection string for runner-side update
#   GIT_SHA, IMAGE        optional metadata for webhook
#   SKIP_IF_UNCONFIGURED  if 1 (default), exit 0 when neither webhook nor connection set
#
# Docs: docs/DATABASE_MIGRATION_STRATEGY.md

set -euo pipefail

STAGE="${STAGE:-staging}"
WEBHOOK="${MIGRATE_WEBHOOK_URL:-}"
CONN="${DATABASE_CONNECTION:-}"
GIT_SHA="${GIT_SHA:-}"
IMAGE="${IMAGE:-}"
SKIP_IF_UNCONFIGURED="${SKIP_IF_UNCONFIGURED:-1}"

echo "Backend migrations"
echo "=================="
echo "STAGE=${STAGE}"

if [[ -n "${WEBHOOK}" ]]; then
  echo "Calling migrate webhook…"
  curl -sS -X POST \
    -H 'Content-Type: application/json' \
    --data "{\"action\":\"migrate\",\"stage\":\"${STAGE}\",\"sha\":\"${GIT_SHA}\",\"image\":\"${IMAGE}\"}" \
    "${WEBHOOK}"
  echo
  echo "Migrate webhook invoked."
  exit 0
fi

if [[ -n "${CONN}" ]]; then
  echo "Running dotnet ef database update on runner…"
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet not on PATH" >&2
    exit 2
  fi
  # shellcheck disable=SC2086
  dotnet ef database update \
    --project backend/KasseAPI_Final.csproj \
    --startup-project backend/KasseAPI_Final.csproj \
    --configuration Release \
    --connection "${CONN}"
  echo "database update finished."
  exit 0
fi

if [[ "${SKIP_IF_UNCONFIGURED}" == "1" ]]; then
  echo "No MIGRATE_WEBHOOK_URL or DATABASE_CONNECTION — skipping (ops must migrate on host)."
  echo "See docs/DATABASE_MIGRATION_STRATEGY.md"
  exit 0
fi

echo "ERROR: migrations required but no webhook/connection configured" >&2
exit 2
