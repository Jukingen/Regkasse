#!/usr/bin/env bash
# Pre-deployment checks for a Linux production host (filesystem layout).
#
# Usage (on the server, as deploy user or root):
#   ./scripts/ops/preflight-production.sh
#
# Env overrides:
#   REGKASSE_ROOT          default /var/www/regkasse
#   API_PUBLISH_DIR        default $REGKASSE_ROOT/api  (or $REGKASSE_ROOT/backend)
#   BACKUP_STAGING         default /var/backups/regkasse/staging
#   BACKUP_ARCHIVE         default /var/backups/regkasse/archive
#   PG_DUMP_PATH           default $(command -v pg_dump) or /usr/bin/pg_dump
#   SYSTEMD_UNIT           default regkasse-api
#   REQUIRE_SYSTEMD=0      skip unit checks
#
# Exit 0 = ready enough to proceed; non-zero = fix issues first.
# Docs: docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md

set -euo pipefail

REGKASSE_ROOT="${REGKASSE_ROOT:-/var/www/regkasse}"
API_PUBLISH_DIR="${API_PUBLISH_DIR:-}"
if [[ -z "${API_PUBLISH_DIR}" ]]; then
  if [[ -d "${REGKASSE_ROOT}/api" ]]; then
    API_PUBLISH_DIR="${REGKASSE_ROOT}/api"
  else
    API_PUBLISH_DIR="${REGKASSE_ROOT}/backend"
  fi
fi
BACKUP_STAGING="${BACKUP_STAGING:-/var/backups/regkasse/staging}"
BACKUP_ARCHIVE="${BACKUP_ARCHIVE:-/var/backups/regkasse/archive}"
SYSTEMD_UNIT="${SYSTEMD_UNIT:-regkasse-api}"
REQUIRE_SYSTEMD="${REQUIRE_SYSTEMD:-1}"
FAIL=0

ok() { echo "  OK  $*"; }
warn() { echo "  WARN $*"; }
bad() { echo "  FAIL $*"; FAIL=1; }

echo "Regkasse production preflight"
echo "============================="
echo "Host:            $(hostname -f 2>/dev/null || hostname)"
echo "UTC:             $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "REGKASSE_ROOT:   ${REGKASSE_ROOT}"
echo "API_PUBLISH_DIR: ${API_PUBLISH_DIR}"
echo

echo "== Environment =="
ENV_NAME="${ASPNETCORE_ENVIRONMENT:-}"
if [[ -z "${ENV_NAME}" ]]; then
  # systemd EnvironmentFile / drop-in often not exported to this shell
  if [[ -f "/etc/systemd/system/${SYSTEMD_UNIT}.service" ]] || systemctl cat "${SYSTEMD_UNIT}" >/dev/null 2>&1; then
    ENV_NAME="$(systemctl show -p Environment "${SYSTEMD_UNIT}" 2>/dev/null | tr ' ' '\n' | sed -n 's/^ASPNETCORE_ENVIRONMENT=//p' | head -1 || true)"
  fi
fi
if [[ "${ENV_NAME}" == "Production" ]]; then
  ok "ASPNETCORE_ENVIRONMENT=Production"
elif [[ -n "${ENV_NAME}" ]]; then
  bad "ASPNETCORE_ENVIRONMENT=${ENV_NAME} (expected Production for this host)"
else
  warn "ASPNETCORE_ENVIRONMENT not visible in this shell — confirm systemd unit sets Production"
fi

echo
echo "== PostgreSQL / pg_dump =="
if command -v psql >/dev/null 2>&1; then
  ok "psql: $(psql --version | head -1)"
else
  warn "psql not on PATH (client tools may still be under /usr/lib/postgresql/*/bin)"
fi

PG_DUMP_PATH="${PG_DUMP_PATH:-}"
if [[ -z "${PG_DUMP_PATH}" ]]; then
  PG_DUMP_PATH="$(command -v pg_dump 2>/dev/null || true)"
fi
if [[ -z "${PG_DUMP_PATH}" && -x /usr/bin/pg_dump ]]; then
  PG_DUMP_PATH=/usr/bin/pg_dump
fi
if [[ -n "${PG_DUMP_PATH}" && -x "${PG_DUMP_PATH}" ]]; then
  ok "pg_dump: ${PG_DUMP_PATH} ($("${PG_DUMP_PATH}" --version | head -1))"
else
  bad "pg_dump not found — install postgresql-client and set Backup:PgDumpExecutablePath"
fi

echo
echo "== Backup directories =="
for d in "${BACKUP_STAGING}" "${BACKUP_ARCHIVE}"; do
  if [[ -d "${d}" ]]; then
    if [[ -w "${d}" ]]; then
      ok "writable ${d}"
    else
      bad "not writable: ${d}"
    fi
  else
    bad "missing directory: ${d} (mkdir -p and chown to API user)"
  fi
done

if command -v df >/dev/null 2>&1; then
  echo
  echo "== Disk space =="
  df -h "${BACKUP_STAGING}" 2>/dev/null || df -h /var/backups 2>/dev/null || df -h /
  AVAIL_KB="$(df -Pk "${BACKUP_STAGING}" 2>/dev/null | awk 'NR==2 {print $4}' || echo 0)"
  # 10 GiB ≈ 10485760 KiB
  if [[ "${AVAIL_KB}" =~ ^[0-9]+$ ]] && (( AVAIL_KB < 10485760 )); then
    warn "staging volume has < ~10 GiB free (${AVAIL_KB} KiB) — review retention / disk"
  else
    ok "staging free space check (>= ~10 GiB or unknown)"
  fi
fi

echo
echo "== Publish tree / secrets =="
if [[ -d "${API_PUBLISH_DIR}" ]]; then
  ok "publish dir exists: ${API_PUBLISH_DIR}"
else
  warn "publish dir missing (first deploy?): ${API_PUBLISH_DIR}"
fi

PROD_SETTINGS="${API_PUBLISH_DIR}/appsettings.Production.json"
if [[ -f "${PROD_SETTINGS}" ]]; then
  ok "appsettings.Production.json present (do not commit this file)"
  if grep -qE '"ExecutionAdapterKind"[[:space:]]*:[[:space:]]*"PgDump"' "${PROD_SETTINGS}" 2>/dev/null \
    || grep -q 'Backup__ExecutionAdapterKind=PgDump' /etc/regkasse/*.env 2>/dev/null; then
    ok "Backup ExecutionAdapterKind looks like PgDump (file or env)"
  else
    warn "Could not confirm ExecutionAdapterKind=PgDump in ${PROD_SETTINGS} — verify env Backup__ExecutionAdapterKind"
  fi
  if grep -qiE 'Soft|Demo|Fake|"UseSimulation"[[:space:]]*:[[:space:]]*true' "${PROD_SETTINGS}" 2>/dev/null; then
    bad "Production settings may still contain Soft/Demo/Fake TSE or UseSimulation=true — abort fiscal go-live"
  fi
else
  warn "No appsettings.Production.json under publish dir — ensure secrets via env / EnvironmentFile"
fi

echo
echo "== systemd =="
if [[ "${REQUIRE_SYSTEMD}" == "1" ]]; then
  if systemctl cat "${SYSTEMD_UNIT}" >/dev/null 2>&1; then
    ok "unit ${SYSTEMD_UNIT} exists"
    systemctl is-active --quiet "${SYSTEMD_UNIT}" && ok "${SYSTEMD_UNIT} is active" || warn "${SYSTEMD_UNIT} not active"
  else
    warn "systemd unit ${SYSTEMD_UNIT} not found (set SYSTEMD_UNIT or REQUIRE_SYSTEMD=0)"
  fi
else
  ok "systemd checks skipped (REQUIRE_SYSTEMD=0)"
fi

echo
echo "== TLS / public health (optional) =="
API_BASE="${API_BASE:-https://api.regkasse.at}"
if curl -fsS --connect-timeout 5 "${API_BASE}/api/health/live" >/dev/null 2>&1; then
  ok "live: ${API_BASE}/api/health/live"
else
  warn "could not reach ${API_BASE}/api/health/live (DNS/TLS/firewall or first deploy)"
fi

echo
if (( FAIL == 0 )); then
  echo "Preflight finished: PASS (with possible WARNs)."
  exit 0
fi
echo "Preflight finished: FAIL — fix FAIL lines before deploy."
exit 1
