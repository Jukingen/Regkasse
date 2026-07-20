#!/usr/bin/env bash
# Restore Regkasse from a pre-deploy backup under /var/www/regkasse/backup/<stamp>.
#
# Usage (on production server):
#   sudo ./scripts/rollback-production.sh              # uses backup/LATEST
#   sudo ./scripts/rollback-production.sh 20260719-120000
#
# Env:
#   REGKASSE_ROOT   default /var/www/regkasse
#   BACKUP_ROOT     default $REGKASSE_ROOT/backup
#   SKIP_RESTART=1  restore files only (no systemctl)
#
# Stops on first failure. Does not touch PostgreSQL / EF migrations
# (schema rollback is a separate, high-risk procedure).

set -euo pipefail

REGKASSE_ROOT="${REGKASSE_ROOT:-/var/www/regkasse}"
BACKUP_ROOT="${BACKUP_ROOT:-${REGKASSE_ROOT}/backup}"
SKIP_RESTART="${SKIP_RESTART:-0}"

STAMP="${1:-}"
if [[ -z "${STAMP}" ]]; then
  if [[ ! -f "${BACKUP_ROOT}/LATEST" ]]; then
    echo "ERROR: No stamp argument and ${BACKUP_ROOT}/LATEST missing." >&2
    exit 1
  fi
  STAMP="$(tr -d '[:space:]' < "${BACKUP_ROOT}/LATEST")"
fi

SRC="${BACKUP_ROOT}/${STAMP}"
if [[ ! -d "${SRC}" ]]; then
  echo "ERROR: Backup not found: ${SRC}" >&2
  echo "Available:" >&2
  ls -1 "${BACKUP_ROOT}" 2>/dev/null || true
  exit 1
fi

echo "Regkasse production rollback"
echo "============================"
echo "Stamp:  ${STAMP}"
echo "Source: ${SRC}"
echo "Target: ${REGKASSE_ROOT}"
echo

confirm_prompt() {
  if [[ "${REGKASSE_ROLLBACK_CONFIRM:-}" == "YES" ]]; then
    return 0
  fi
  read -r -p "Type YES to restore this backup over live paths: " answer
  if [[ "${answer}" != "YES" ]]; then
    echo "Aborted."
    exit 1
  fi
}

confirm_prompt

rollback_component() {
  local name="$1"
  local src_dir="${SRC}/${name}"
  local dst_dir="${REGKASSE_ROOT}/${name}"

  if [[ ! -d "${src_dir}" ]]; then
    echo "SKIP ${name} (not in backup)"
    return 0
  fi

  mkdir -p "${dst_dir}"
  # Preserve live secrets that were excluded from backup archives.
  rsync -a --delete \
    --exclude 'appsettings.Production.json' \
    --exclude 'appsettings.Development.json' \
    --exclude '.env' \
    --exclude '.env.local' \
    --exclude '.env.production' \
    --exclude 'node_modules/' \
    "${src_dir}/" "${dst_dir}/"
  echo "OK restored ${name}"
}

rollback_component backend
rollback_component frontend-admin
rollback_component frontend

if [[ "${SKIP_RESTART}" == "1" ]]; then
  echo
  echo "SKIP_RESTART=1 — not restarting services."
  echo "Manual restart when ready:"
  echo "  systemctl restart regkasse-api"
  echo "  systemctl restart regkasse-fa"
  echo "  systemctl restart regkasse-pos"
  exit 0
fi

echo
echo "Restarting services..."
systemctl restart regkasse-api
systemctl restart regkasse-fa
systemctl restart regkasse-pos

echo
echo "Waiting for API health..."
sleep 3
if curl -fsS --connect-timeout 5 "http://127.0.0.1:5184/api/health" >/dev/null 2>&1 \
  || curl -fsS --connect-timeout 5 "http://127.0.0.1/api/health" >/dev/null 2>&1; then
  echo "OK health check"
else
  echo "WARN: health check did not return 200 — inspect journalctl -u regkasse-api" >&2
  exit 1
fi

echo
echo "Rollback complete (stamp=${STAMP})."
echo "Verify: ./scripts/e2e-smoke-test.sh  (set API_BASE to production API)"
