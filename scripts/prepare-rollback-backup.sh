#!/usr/bin/env bash
# Archive the currently deployed Regkasse release before overwriting (rollback safety net).
#
# On the production server (run BEFORE scp/rsync of a new release):
#   sudo ./scripts/prepare-rollback-backup.sh
#
# Env:
#   REGKASSE_ROOT   default /var/www/regkasse
#   BACKUP_ROOT     default $REGKASSE_ROOT/backup
#
# Creates: $BACKUP_ROOT/<yyyyMMdd-HHmmss>/{backend,frontend-admin,frontend}/
# Writes:  $BACKUP_ROOT/LATEST  (stamp of newest backup)

set -euo pipefail

REGKASSE_ROOT="${REGKASSE_ROOT:-/var/www/regkasse}"
BACKUP_ROOT="${BACKUP_ROOT:-${REGKASSE_ROOT}/backup}"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
DEST="${BACKUP_ROOT}/${STAMP}"

echo "Regkasse rollback backup"
echo "========================"
echo "Root:   ${REGKASSE_ROOT}"
echo "Dest:   ${DEST}"
echo

if [[ ! -d "${REGKASSE_ROOT}" ]]; then
  echo "ERROR: REGKASSE_ROOT does not exist: ${REGKASSE_ROOT}" >&2
  exit 1
fi

mkdir -p "${DEST}/backend" "${DEST}/frontend-admin" "${DEST}/frontend"

# Backend publish tree (exclude local secrets if present in tree)
if [[ -d "${REGKASSE_ROOT}/backend" ]]; then
  rsync -a \
    --exclude 'appsettings.Production.json' \
    --exclude 'appsettings.Development.json' \
    --exclude 'tmp-build-out/' \
    --exclude 'build-test-out/' \
    --exclude 'build-migrate-out/' \
    --exclude 'Tests/' \
    "${REGKASSE_ROOT}/backend/" "${DEST}/backend/"
  echo "OK backend"
else
  echo "SKIP backend (missing)"
fi

# FA — typically .next (+ package.json / next.config for restart)
if [[ -d "${REGKASSE_ROOT}/frontend-admin" ]]; then
  rsync -a \
    --exclude 'node_modules/' \
    --exclude '.env*' \
    "${REGKASSE_ROOT}/frontend-admin/" "${DEST}/frontend-admin/"
  echo "OK frontend-admin"
else
  echo "SKIP frontend-admin (missing)"
fi

# POS static export (dist contents often live under frontend/)
if [[ -d "${REGKASSE_ROOT}/frontend" ]]; then
  rsync -a \
    --exclude 'node_modules/' \
    --exclude '.env*' \
    "${REGKASSE_ROOT}/frontend/" "${DEST}/frontend/"
  echo "OK frontend (POS)"
else
  echo "SKIP frontend (missing)"
fi

cat > "${DEST}/MANIFEST.txt" <<EOF
stamp=${STAMP}
createdAtUtc=$(date -u +%Y-%m-%dT%H:%M:%SZ)
regkasseRoot=${REGKASSE_ROOT}
note=Pre-deploy rollback archive. Secrets (appsettings.Production.json, .env*) intentionally excluded.
EOF

printf '%s\n' "${STAMP}" > "${BACKUP_ROOT}/LATEST"
echo
echo "Backup complete: ${DEST}"
echo "LATEST -> ${STAMP}"
