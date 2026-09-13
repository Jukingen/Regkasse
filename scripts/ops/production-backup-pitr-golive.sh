#!/usr/bin/env bash
# Production GO_LIVE — WAL / System backup / isolated restore drill / PITR dry-run
#
# Run ON the Production host (or a jump box that can reach the API and the DB).
# Never points pg_restore at Production DefaultConnection / kasse_prod.
#
# Required:
#   SUPERADMIN_JWT   Super Admin bearer token
# Optional:
#   API_BASE         default https://api.regkasse.at
#   WAL_DIR          default /var/lib/postgresql/wal-archive
#   STAGING_ROOT     default /var/backups/regkasse/staging
#   ARCHIVE_ROOT     default /var/backups/regkasse/archive
#   COMPOSE_FILE     default docker-compose.prod.yml
#   ENV_FILE         default .env.production
#
# Docs: docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md §4.1–§4.2
set -euo pipefail

API_BASE="${API_BASE:-https://api.regkasse.at}"
WAL_DIR="${WAL_DIR:-/var/lib/postgresql/wal-archive}"
STAGING_ROOT="${STAGING_ROOT:-/var/backups/regkasse/staging}"
ARCHIVE_ROOT="${ARCHIVE_ROOT:-/var/backups/regkasse/archive}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 1; }; }
need curl
need jq
need xxd

if [[ -z "${SUPERADMIN_JWT:-}" ]]; then
  echo "Set SUPERADMIN_JWT first (do not commit it)." >&2
  exit 1
fi

# Production CSRF is enabled: cookie + X-XSRF-TOKEN (native clients also send X-CSRF-COOKIE).
CSRF_JSON="$(curl -fsS "$API_BASE/api/csrf/token")"
CSRF_TOKEN="$(echo "$CSRF_JSON" | jq -r '.token')"
if [[ -z "$CSRF_TOKEN" || "$CSRF_TOKEN" == "null" ]]; then
  echo "Could not obtain CSRF token from $API_BASE/api/csrf/token" >&2
  exit 1
fi
auth=(
  -H "Authorization: Bearer $SUPERADMIN_JWT"
  -H "Accept: application/json"
  -H "X-XSRF-TOKEN: $CSRF_TOKEN"
  -H "X-CSRF-COOKIE: $CSRF_TOKEN"
)

echo "=== 0) API live ==="
curl -fsS "$API_BASE/api/health/live"
echo

echo "=== 1) WAL archive directory + Postgres settings (DB host) ==="
echo "If this is the API container only, run the psql/systemctl block on the PostgreSQL host."
sudo mkdir -p "$WAL_DIR"
sudo chown postgres:postgres "$WAL_DIR" 2>/dev/null || true
sudo chmod 750 "$WAL_DIR" 2>/dev/null || true

if command -v psql >/dev/null 2>&1 && id postgres >/dev/null 2>&1; then
  sudo -u postgres psql -c "ALTER SYSTEM SET wal_level = 'replica';"
  sudo -u postgres psql -c "ALTER SYSTEM SET archive_mode = 'on';"
  sudo -u postgres psql -c "ALTER SYSTEM SET archive_timeout = 300;"
  sudo -u postgres psql -c "ALTER SYSTEM SET archive_command = 'test ! -f ${WAL_DIR}/%f && cp %p ${WAL_DIR}/%f';"
  echo "Restart PostgreSQL now (archive_mode / wal_level need restart):"
  echo "  sudo systemctl restart postgresql"
  echo "  # or: docker compose -f $COMPOSE_FILE --env-file $ENV_FILE restart postgres"
  read -r -p "Press Enter AFTER PostgreSQL has been restarted..."
  sudo -u postgres psql -c "SHOW wal_level; SHOW archive_mode; SHOW archive_timeout; SHOW archive_command;"
  sudo -u postgres psql -c "SELECT pg_switch_wal();"
  sleep 5
  ls -la "$WAL_DIR" | head
else
  echo "psql/postgres user not on this host — apply ALTER SYSTEM + restart on the DB host, then:"
  echo "  ls -la $WAL_DIR"
fi

echo
echo "=== 2) Confirm API env (do not print secrets) ==="
echo "Ensure these are set, then recreate the API container:"
cat <<EOF
Backup__WalArchiveDirectory=$WAL_DIR
Backup__WalArchiveRetentionDays=7
Backup__WalArchiveSwitchIntervalMinutes=5
Backup__PitrWalArchivingDeclaredEnabled=true
Backup__IncrementalBackupEnabled=true
Backup__IncrementalBackupCron=0 3 * * *
EOF
echo "Example restart:"
echo "  docker compose -f $COMPOSE_FILE --env-file $ENV_FILE up -d backend"
read -r -p "Press Enter AFTER the API has been restarted..."

echo
echo "=== 2b) PITR WAL inventory ==="
curl -fsS "${auth[@]}" "$API_BASE/api/admin/backup/pitr/wal" | jq .
FILE_COUNT="$(curl -fsS "${auth[@]}" "$API_BASE/api/admin/backup/pitr/wal" | jq -r '.fileCount // 0')"
if [[ "$FILE_COUNT" -lt 1 ]]; then
  echo "WARN: fileCount is 0. Incrementals/PITR WAL coverage is not proven yet. Continue only if archive_command is still catching up (wait archive_timeout=300s)." >&2
fi

echo
echo "=== 3) Trigger System backup ==="
TRIGGER="$(curl -fsS -X POST "${auth[@]}" -H "Content-Type: application/json" \
  "$API_BASE/api/settings/backup/now")"
echo "$TRIGGER" | jq .
RUN_ID="$(echo "$TRIGGER" | jq -r '.run.id // .runId // empty')"
if [[ -z "$RUN_ID" || "$RUN_ID" == "null" ]]; then
  echo "Could not parse run id from trigger response." >&2
  exit 1
fi
echo "RUN_ID=$RUN_ID"

echo "Polling until Succeeded (3) / Failed (4) / VerificationFailed (5)..."
for _ in $(seq 1 90); do
  RUN_JSON="$(curl -fsS "${auth[@]}" "$API_BASE/api/admin/backup/runs/$RUN_ID")"
  STATUS="$(echo "$RUN_JSON" | jq -r '.status')"
  echo "  status=$STATUS"
  case "$STATUS" in
    3|Succeeded) break ;;
    4|Failed|5|VerificationFailed)
      echo "$RUN_JSON" | jq .
      echo "Backup did not succeed." >&2
      exit 1
      ;;
  esac
  sleep 10
done
echo "$RUN_JSON" | jq '{id,status,strategy,adapterKind,completedAt,failureCode}'

echo
echo "=== 3b) PGDMP magic + archive copy ==="
DUMP="$(ls -1t "$STAGING_ROOT"/backup_deployment_system_*.dump 2>/dev/null | head -n1 || true)"
if [[ -z "$DUMP" ]]; then
  echo "No staging dump matching backup_deployment_system_*.dump under $STAGING_ROOT" >&2
  echo "Look for logical_*.dump if the host uses the older name."
  DUMP="$(ls -1t "$STAGING_ROOT"/*.dump 2>/dev/null | head -n1 || true)"
fi
if [[ -n "$DUMP" ]]; then
  echo "DUMP=$DUMP"
  head -c 5 "$DUMP" | xxd
  echo "Expect 50 47 44 4d 50 (PGDMP) or 52 4b 42 41 4b (RKBAK encrypted)"
else
  echo "WARN: dump file not visible from this host (API may run in another container)." >&2
fi
RUN_NODASH="${RUN_ID//-/}"
echo "Archive dir: $ARCHIVE_ROOT/$RUN_NODASH"
ls -la "$ARCHIVE_ROOT/$RUN_NODASH" 2>/dev/null || echo "WARN: archive dir not on this host."

echo
echo "=== 3c) Checksum verify ==="
curl -fsS -X POST "${auth[@]}" "$API_BASE/api/admin/backup/$RUN_ID/verify" | jq '{isValid,failureReason,artifacts}'

echo
echo "=== 4) Isolated restore drill (latest eligible System dump) ==="
echo "Does not accept backupRunId. Never restores DefaultConnection."
DRILL="$(curl -fsS -X POST "${auth[@]}" -H "Content-Type: application/json" \
  "$API_BASE/api/admin/restore-verification/trigger" -d '{}')"
echo "$DRILL" | jq .
DRILL_ID="$(echo "$DRILL" | jq -r '.runId // .run.id')"
echo "DRILL_ID=$DRILL_ID"

for _ in $(seq 1 90); do
  DRILL_JSON="$(curl -fsS "${auth[@]}" "$API_BASE/api/admin/restore-verification/runs/$DRILL_ID")"
  DSTATUS="$(echo "$DRILL_JSON" | jq -r '.status')"
  echo "  drill status=$DSTATUS"
  case "$DSTATUS" in
    2|Succeeded) break ;;
    3|Failed)
      echo "$DRILL_JSON" | jq .
      echo "Restore drill failed." >&2
      exit 1
      ;;
  esac
  sleep 10
done
echo "$DRILL_JSON" | jq '{
  id,status,sourceBackupRunId,dumpInspectionPassed,
  restoreAttemptExecuted,restoreAttemptPassed,restoreTargetDbRedacted,
  postRestoreContinuityChecksPassed,postRestoreL4ContinuityProofState,fiscalContinuityLayerPassed,
  fiscalSqlSkipped,fiscalSqlSkipReason,fiscalSqlPassed
}'

echo
echo "=== 4b) Fiscal SQL on clone (if still up) — never on kasse_prod ==="
CLONE="$(echo "$DRILL_JSON" | jq -r '.restoreTargetDbRedacted // empty')"
echo "Clone name from API (redacted): $CLONE"
echo "If the worker already DROPped rv_v_*, keep a labelled clone and run:"
echo "  psql -d <clone> -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql"
echo "Expect go_live_summary: RESULT: OK"

echo
echo "=== 5) PITR validate + dry-run ==="
TARGET="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "TARGET=$TARGET"
curl -fsS "${auth[@]}" "$API_BASE/api/admin/backup/pitr/availability" | jq '{isAvailable,earliestRestorePointUtc,latestRestorePointUtc,walArchivingEnabled,walFileCount}'
curl -fsS "${auth[@]}" "$API_BASE/api/admin/backup/pitr/chain" | jq '{restorePointAvailable,walFileCount,fullBackup,incrementals: (.incrementals|length), systemBackups: (.systemBackups|length)}'
curl -fsS -X POST "${auth[@]}" -H "Content-Type: application/json" \
  "$API_BASE/api/admin/backup/pitr/validate" \
  -d "{\"targetTimeUtc\":\"$TARGET\"}" | jq .
curl -fsS -X POST "${auth[@]}" -H "Content-Type: application/json" \
  "$API_BASE/api/admin/backup/pitr/pre-restore-validate" \
  -d "{\"targetTimeUtc\":\"$TARGET\"}" | jq '{passed,recoveryMethod,estimatedDataLossSeconds,hash,schema,tseChain,message}'
DRY="$(curl -fsS -X POST "${auth[@]}" -H "Content-Type: application/json" \
  "$API_BASE/api/admin/backup/pitr/dry-run" \
  -d "{\"targetTimeUtc\":\"$TARGET\"}")"
echo "$DRY" | jq .
echo
echo "=== 6) Evidence (paste into docs/BACKUP_RESTORE_DRILL_EVIDENCE.md) ==="
cat <<EOF
| $(date -u +"%Y-%m-%d %H:%M") | (operator) | \`$RUN_ID\` | \`$CLONE\` | dump magic see xxd above | (counts) | (fiscal RESULT) | skipped or documented | (PASSED/FAILED) | Production host. WAL fileCount=$FILE_COUNT. Drill \`$DRILL_ID\`. PITR dry-run accepted=$(echo "$DRY" | jq -r '.accepted'). DefaultConnection was not restored onto. |
EOF
echo "Do not mark GO_LIVE PASSED until this row is filled and §8 is signed."
