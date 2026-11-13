#!/usr/bin/env bash
# Regkasse API E2E smoke — health, FA login, authenticated RKSV status.
# Requires a running API (default http://localhost:5184).
#
# Usage (repo root or any cwd):
#   ./scripts/e2e-smoke-test.sh
#   API_BASE=http://localhost:5184 TENANT_ID=dev ./scripts/e2e-smoke-test.sh
#
# Env overrides:
#   API_BASE, TENANT_ID, LOGIN_IDENTIFIER, LOGIN_PASSWORD

set -uo pipefail

API_BASE="${API_BASE:-http://localhost:5184}"
TENANT_ID="${TENANT_ID:-dev}"
LOGIN_IDENTIFIER="${LOGIN_IDENTIFIER:-admin@admin.com}"
LOGIN_PASSWORD="${LOGIN_PASSWORD:-Admin123!}"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

PASSED=0
FAILED=0

pass() {
  echo -e "${GREEN}OK${NC}"
  PASSED=$((PASSED + 1))
}

fail() {
  local detail="${1:-}"
  echo -e "${RED}FAILED${NC}${detail:+ — $detail}"
  FAILED=$((FAILED + 1))
}

echo "Regkasse E2E Smoke Test"
echo "======================="
echo -e "${YELLOW}API${NC}: ${API_BASE}  ${YELLOW}tenant${NC}: ${TENANT_ID}"
echo

# --- Backend Health ---
echo -n "Backend Health: "
HEALTH_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "${API_BASE}/api/health" 2>/dev/null || true)
HEALTH_CODE="${HEALTH_CODE:-000}"
if [ "$HEALTH_CODE" = "200" ]; then
  pass
else
  fail "HTTP ${HEALTH_CODE} (is the API running?)"
fi

# --- FA Login ---
echo -n "FA Login: "
LOGIN_BODY=$(curl -s --connect-timeout 10 -X POST "${API_BASE}/api/Auth/login" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: ${TENANT_ID}" \
  -d "{\"loginIdentifier\":\"${LOGIN_IDENTIFIER}\",\"password\":\"${LOGIN_PASSWORD}\",\"clientApp\":\"admin\"}" \
  2>/dev/null || true)

TOKEN=""
if command -v node >/dev/null 2>&1; then
  TOKEN=$(LOGIN_BODY="$LOGIN_BODY" node -e '
    try {
      const j = JSON.parse(process.env.LOGIN_BODY || "");
      if (j && typeof j.token === "string") process.stdout.write(j.token);
    } catch { /* ignore */ }
  ' 2>/dev/null || true)
else
  TOKEN=$(printf '%s' "$LOGIN_BODY" | sed -n 's/.*"token"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)
fi

if [ -n "$TOKEN" ]; then
  pass
else
  fail "no token in response"
fi

# --- RKSV Status (authorized) ---
echo -n "RKSV Status: "
if [ -z "$TOKEN" ]; then
  fail "skipped (no login token)"
else
  RKSV_BODY=$(curl -s --connect-timeout 10 -X GET "${API_BASE}/api/rksv/status" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "X-Tenant-Id: ${TENANT_ID}" \
    2>/dev/null || true)

  if printf '%s' "$RKSV_BODY" | grep -q '"isSimulated"'; then
    pass
  else
    fail "expected isSimulated in JSON"
  fi
fi

echo
echo "Summary: ${PASSED} passed, ${FAILED} failed"

if [ "$FAILED" -gt 0 ]; then
  echo -e "${RED}E2E smoke failed.${NC}"
  exit 1
fi

echo -e "${GREEN}All E2E smoke checks passed.${NC}"
exit 0
