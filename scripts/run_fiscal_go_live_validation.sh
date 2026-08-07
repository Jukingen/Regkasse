#!/usr/bin/env bash
# =============================================================================
# Wrapper for scripts/sql/fiscal_go_live_validation.sql — CI/release gate.
# - Runs SQL via psql; FAIL → exit 1 (pipeline fail); WARN → exit 0, report saved as artifact.
# - Does not modify validation semantics; only enforces execution discipline.
# =============================================================================
set -euo pipefail

REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
SQL_FILE="${REPO_ROOT}/scripts/sql/fiscal_go_live_validation.sql"
REPORT_PATH="${FISCAL_VALIDATION_REPORT_PATH:-${REPO_ROOT}/fiscal_validation_report.txt}"

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "ERROR: DATABASE_URL is not set. Set it to a PostgreSQL connection URL (e.g. postgresql://user:pass@host:5432/dbname)."
  exit 1
fi

if [[ ! -f "$SQL_FILE" ]]; then
  echo "ERROR: SQL file not found: $SQL_FILE"
  exit 1
fi

echo "Running fiscal go-live validation (read-only)..."
echo "  SQL: $SQL_FILE"
echo "  Report: $REPORT_PATH"

if ! psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$SQL_FILE" > "$REPORT_PATH" 2>&1; then
  echo "--- Validation output (psql failed) ---"
  cat "$REPORT_PATH"
  echo "---"
  echo "Fiscal validation: psql exited with error (e.g. connection or SQL error). Pipeline failed."
  exit 1
fi

# Always print full output to stdout for CI logs
cat "$REPORT_PATH"

if grep -q "RESULT: FAIL" "$REPORT_PATH"; then
  echo ""
  echo "Fiscal validation: FAIL — do not go-live until resolved. Pipeline failed."
  exit 1
fi

if grep -q "RESULT: WARN" "$REPORT_PATH"; then
  echo ""
  echo "Fiscal validation: WARN — review before go-live. Full report saved to: $REPORT_PATH"
  echo "  (In CI, upload this file as an artifact.)"
  exit 0
fi

echo ""
echo "Fiscal validation: OK — no FAIL/WARN."
exit 0
