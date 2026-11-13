#!/usr/bin/env bash
# Filesystem production deploy for Regkasse API (Linux host).
#
# Typical layout:
#   /var/www/regkasse/api          published API (or .../backend)
#   /var/www/regkasse/backup       prepare-rollback-backup stamps
#   systemd unit: regkasse-api
#
# Usage (from a checkout that contains scripts/ + backend/):
#   export REGKASSE_DEPLOY_CONFIRM=YES
#   export CONNECTION_STRING='Host=…;Database=…;Username=…;Password=…'   # for ef migrate
#   sudo -E ./scripts/ops/deploy-production.sh
#
# Optional env:
#   REGKASSE_ROOT=/var/www/regkasse
#   API_PUBLISH_DIR=$REGKASSE_ROOT/api
#   SOURCE_REPO=/path/to/git/checkout     # default: repo root of this script
#   SKIP_PREFLIGHT=0|1
#   SKIP_ROLLBACK_ARCHIVE=0|1
#   SKIP_BUILD=0|1
#   SKIP_MIGRATE=0|1
#   SKIP_RESTART=0|1
#   SKIP_SMOKE=0|1
#   SYSTEMD_UNIT=regkasse-api
#   API_BASE=https://api.regkasse.at
#   DOTNET_CONFIGURATION=Release
#
# Secrets:
#   Do NOT commit appsettings.Production.json. Copy from a secure store after publish,
#   or inject Backup__/ConnectionStrings__/JwtSettings__/Tse__ via EnvironmentFile.
#   Template: backend/appsettings.Production.example.json
#
# Rollback:
#   sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
#
# Docs: docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md · DEPLOYMENT.md · docs/DEPLOYMENT_SMOKE_TEST.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SOURCE_REPO="${SOURCE_REPO:-${REPO_ROOT}}"

REGKASSE_ROOT="${REGKASSE_ROOT:-/var/www/regkasse}"
if [[ -d "${REGKASSE_ROOT}/api" || "${API_PUBLISH_DIR:-}" == */api ]]; then
  API_PUBLISH_DIR="${API_PUBLISH_DIR:-${REGKASSE_ROOT}/api}"
else
  API_PUBLISH_DIR="${API_PUBLISH_DIR:-${REGKASSE_ROOT}/backend}"
fi

SYSTEMD_UNIT="${SYSTEMD_UNIT:-regkasse-api}"
API_BASE="${API_BASE:-https://api.regkasse.at}"
DOTNET_CONFIGURATION="${DOTNET_CONFIGURATION:-Release}"
SKIP_PREFLIGHT="${SKIP_PREFLIGHT:-0}"
SKIP_ROLLBACK_ARCHIVE="${SKIP_ROLLBACK_ARCHIVE:-0}"
SKIP_BUILD="${SKIP_BUILD:-0}"
SKIP_MIGRATE="${SKIP_MIGRATE:-0}"
SKIP_RESTART="${SKIP_RESTART:-0}"
SKIP_SMOKE="${SKIP_SMOKE:-0}"

die() { echo "ERROR: $*" >&2; exit 1; }
warn() { echo "WARN: $*" >&2; }

if [[ "${REGKASSE_DEPLOY_CONFIRM:-}" != "YES" ]]; then
  cat <<EOF >&2
Refusing to deploy without confirmation.

  export REGKASSE_DEPLOY_CONFIRM=YES
  sudo -E ./scripts/ops/deploy-production.sh

This publishes Release bits to ${API_PUBLISH_DIR}, may run EF migrations, and restarts ${SYSTEMD_UNIT}.
EOF
  exit 1
fi

if [[ "$(id -u)" -ne 0 ]]; then
  warn "not running as root — systemd restart / chown may fail."
fi

echo "Regkasse production deploy"
echo "=========================="
echo "UTC:            $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "SOURCE_REPO:    ${SOURCE_REPO}"
echo "API_PUBLISH_DIR:${API_PUBLISH_DIR}"
echo "SYSTEMD_UNIT:   ${SYSTEMD_UNIT}"
echo "API_BASE:       ${API_BASE}"
echo

cd "${SOURCE_REPO}"

if [[ "${SKIP_PREFLIGHT}" != "1" ]]; then
  echo "== Preflight =="
  bash "${SOURCE_REPO}/scripts/ops/preflight-production.sh" \
    || die "preflight failed — fix FAIL items or SKIP_PREFLIGHT=1 (not recommended)"
  echo
fi

if [[ "${SKIP_ROLLBACK_ARCHIVE}" != "1" ]]; then
  echo "== Pre-deploy rollback archive =="
  if [[ -x "${SOURCE_REPO}/scripts/prepare-rollback-backup.sh" ]]; then
    REGKASSE_ROOT="${REGKASSE_ROOT}" bash "${SOURCE_REPO}/scripts/prepare-rollback-backup.sh"
  else
    die "missing scripts/prepare-rollback-backup.sh"
  fi
  echo
fi

if [[ "${SKIP_BUILD}" != "1" ]]; then
  echo "== Publish API (Release) =="
  command -v dotnet >/dev/null 2>&1 || die "dotnet SDK not found on PATH"
  mkdir -p "${API_PUBLISH_DIR}"
  # Preserve production settings / env-only secrets outside the wipe if present beside publish
  STASH_DIR="$(mktemp -d /tmp/regkasse-prod-settings.XXXXXX)"
  for f in appsettings.Production.json appsettings.Production.json.gpg; do
    if [[ -f "${API_PUBLISH_DIR}/${f}" ]]; then
      cp -a "${API_PUBLISH_DIR}/${f}" "${STASH_DIR}/"
      echo "  stashed ${f}"
    fi
  done

  dotnet publish "${SOURCE_REPO}/backend/KasseAPI_Final.csproj" \
    -c "${DOTNET_CONFIGURATION}" \
    -o "${API_PUBLISH_DIR}" \
    --nologo

  for f in appsettings.Production.json appsettings.Production.json.gpg; do
    if [[ -f "${STASH_DIR}/${f}" ]]; then
      cp -a "${STASH_DIR}/${f}" "${API_PUBLISH_DIR}/"
      echo "  restored ${f}"
    fi
  done
  rm -rf "${STASH_DIR}"

  if [[ ! -f "${API_PUBLISH_DIR}/appsettings.Production.json" ]]; then
    echo "WARN: No appsettings.Production.json in publish output."
    echo "      Copy from secure store or rely on systemd EnvironmentFile (Backup__, ConnectionStrings__, …)."
    echo "      Template: backend/appsettings.Production.example.json"
  fi
  echo
fi

if [[ "${SKIP_MIGRATE}" != "1" ]]; then
  echo "== EF migrations =="
  if [[ -z "${CONNECTION_STRING:-}" && -z "${ConnectionStrings__DefaultConnection:-}" ]]; then
    die "Set CONNECTION_STRING or ConnectionStrings__DefaultConnection for migrations"
  fi
  CS="${CONNECTION_STRING:-${ConnectionStrings__DefaultConnection}}"
  # Prefer dotnet ef from the checkout tooling if available
  if dotnet tool run dotnet-ef --version >/dev/null 2>&1; then
    EF=(dotnet tool run dotnet-ef)
  elif command -v dotnet-ef >/dev/null 2>&1; then
    EF=(dotnet-ef)
  else
    EF=(dotnet ef)
  fi
  "${EF[@]}" database update \
    --project "${SOURCE_REPO}/backend/KasseAPI_Final.csproj" \
    --connection "${CS}"
  echo
fi

if [[ "${SKIP_RESTART}" != "1" ]]; then
  echo "== Restart ${SYSTEMD_UNIT} =="
  systemctl daemon-reload || true
  systemctl restart "${SYSTEMD_UNIT}"
  sleep 3
  systemctl --no-pager --full status "${SYSTEMD_UNIT}" | head -n 20 || true
  echo
fi

echo "== Local health =="
for url in \
  "http://127.0.0.1:5184/api/health/live" \
  "http://127.0.0.1/api/health/live"
do
  if curl -fsS --connect-timeout 3 "${url}" >/dev/null 2>&1; then
    echo "  OK ${url}"
    break
  fi
done

if [[ "${SKIP_SMOKE}" != "1" ]]; then
  echo
  echo "== Public smoke (${API_BASE}) =="
  if [[ -x "${SOURCE_REPO}/scripts/smoke-test.sh" ]]; then
    # Health-only friendly defaults; login secrets optional
    API_BASE="${API_BASE}" \
      REQUIRE_READY="${REQUIRE_READY:-1}" \
      REQUIRE_MIGRATIONS="${REQUIRE_MIGRATIONS:-0}" \
      bash "${SOURCE_REPO}/scripts/smoke-test.sh" \
      || die "smoke-test.sh failed — consider rollback: REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh"
  else
    curl -fsS "${API_BASE}/api/health/live" >/dev/null
    curl -fsS "${API_BASE}/api/health/ready" >/dev/null || warn "ready check failed"
    echo "  OK basic health"
  fi
fi

echo
echo "Deploy finished."
echo "Post-checks (with admin JWT):"
echo "  GET ${API_BASE}/api/admin/backup/execution-mode   # expect PgDump / RealPgDump"
echo "  GET ${API_BASE}/api/admin/rksv/dep-export/status"
echo "  GET ${API_BASE}/api/rksv/environment              # expect Production, not simulation"
echo "Rollback: sudo REGKASSE_ROLLBACK_CONFIRM=YES ${SOURCE_REPO}/scripts/rollback-production.sh"
