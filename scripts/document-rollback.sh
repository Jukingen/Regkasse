#!/usr/bin/env bash
# One-page rollback reference for operators (also see rollback-production.sh).
#
# BEFORE deploy (on server):
#   sudo REGKASSE_ROOT=/var/www/regkasse ./scripts/prepare-rollback-backup.sh
#
# AFTER a bad deploy (on server):
#   sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
#   # or explicit stamp:
#   sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh 20260719-120000
#
# Equivalent manual commands:
#
#   STAMP=$(cat /var/www/regkasse/backup/LATEST)
#   systemctl stop regkasse-api regkasse-fa regkasse-pos
#   rsync -a --delete /var/www/regkasse/backup/$STAMP/backend/ /var/www/regkasse/backend/
#   rsync -a --delete /var/www/regkasse/backup/$STAMP/frontend-admin/ /var/www/regkasse/frontend-admin/
#   rsync -a --delete /var/www/regkasse/backup/$STAMP/frontend/ /var/www/regkasse/frontend/
#   systemctl start regkasse-api regkasse-fa regkasse-pos
#   curl -fsS http://127.0.0.1:5184/api/health
#
# Do NOT roll back EF migrations with this script. Additive migrations stay;
# fiscal/data rollback requires a separate DR procedure (see docs/BACKUP_AND_DISASTER_RECOVERY.md).

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sed -n '1,40p' "$SCRIPT_DIR/rollback-production.sh" | sed -n '/^#/p'
echo
echo "Scripts:"
echo "  $SCRIPT_DIR/prepare-rollback-backup.sh"
echo "  $SCRIPT_DIR/rollback-production.sh"
