#!/usr/bin/env bash
# Regkasse aggregated test runner (backend + FA + API contract; optional E2E).
# Run from repository root:
#   ./scripts/run-all-tests.sh
#   ./scripts/run-all-tests.sh --with-e2e
#
# Env:
#   SKIP_BACKEND=1  SKIP_FA=1  SKIP_CONTRACT=1

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

WITH_E2E=0
for arg in "$@"; do
  case "$arg" in
    --with-e2e) WITH_E2E=1 ;;
    -h|--help)
      echo "Usage: ./scripts/run-all-tests.sh [--with-e2e]"
      exit 0
      ;;
  esac
done

FAILED=0
PASSED_SUITES=0

pass_suite() {
  echo -e "${GREEN}PASS${NC}: $1"
  PASSED_SUITES=$((PASSED_SUITES + 1))
}

fail_suite() {
  echo -e "${RED}FAIL${NC}: $1"
  FAILED=$((FAILED + 1))
}

echo "Regkasse Test Suite"
echo "==================="
echo "Root: $ROOT"
echo

# --- Backend ---
if [ "${SKIP_BACKEND:-0}" = "1" ]; then
  echo -e "${YELLOW}SKIP${NC}: Backend tests (SKIP_BACKEND=1)"
else
  echo "Running Backend Tests..."
  if (
    cd "$ROOT/backend"
    dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --verbosity minimal
  ); then
    pass_suite "Backend (dotnet test)"
  else
    fail_suite "Backend (dotnet test)"
  fi
  echo
fi

# --- Frontend Admin ---
if [ "${SKIP_FA:-0}" = "1" ]; then
  echo -e "${YELLOW}SKIP${NC}: FA tests (SKIP_FA=1)"
else
  echo "Running FA Tests..."
  if (
    cd "$ROOT/frontend-admin"
    npm test -- --passWithNoTests
  ); then
    pass_suite "Frontend Admin (vitest)"
  else
    fail_suite "Frontend Admin (vitest)"
  fi
  echo
fi

# --- API contract smoke ---
if [ "${SKIP_CONTRACT:-0}" = "1" ]; then
  echo -e "${YELLOW}SKIP${NC}: API contract (SKIP_CONTRACT=1)"
else
  echo "Running API Contract Tests..."
  if node "$ROOT/scripts/verify-api-contract.mjs"; then
    pass_suite "API contract (verify-api-contract.mjs)"
  else
    fail_suite "API contract (verify-api-contract.mjs)"
  fi
  echo
fi

# --- Optional E2E (needs running API) ---
if [ "$WITH_E2E" = "1" ]; then
  echo "Running E2E Smoke..."
  if bash "$ROOT/scripts/e2e-smoke-test.sh"; then
    pass_suite "E2E smoke (e2e-smoke-test.sh)"
  else
    fail_suite "E2E smoke (e2e-smoke-test.sh)"
  fi
  echo
else
  echo -e "${YELLOW}SKIP${NC}: E2E smoke (pass --with-e2e when API is running on :5184)"
  echo
fi

echo "==================="
echo "Suites passed: ${PASSED_SUITES}"
echo "Suites failed: ${FAILED}"

if [ "$FAILED" -gt 0 ]; then
  echo -e "${RED}Test suite finished with failures.${NC}"
  exit 1
fi

echo -e "${GREEN}All requested test suites passed.${NC}"
exit 0
