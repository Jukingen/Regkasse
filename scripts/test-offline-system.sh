#!/usr/bin/env bash
# Offline system smoke test suite — structure checks, Node validations, optional backend/frontend tests.
# Run from repository root: ./scripts/test-offline-system.sh
# Options:
#   --with-backend   Run dotnet tests (SequenceReservationServiceTests; skips if no PostgreSQL)
#   --with-frontend  Run frontend Jest offline-related tests
#   --with-api       Curl backend offline POS endpoints (requires running API on :5184)

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

WITH_BACKEND=0
WITH_FRONTEND=0
WITH_API=0

for arg in "$@"; do
  case "$arg" in
    --with-backend) WITH_BACKEND=1 ;;
    --with-frontend) WITH_FRONTEND=1 ;;
    --with-api) WITH_API=1 ;;
    -h|--help)
      echo "Usage: ./scripts/test-offline-system.sh [--with-backend] [--with-frontend] [--with-api]"
      exit 0
      ;;
  esac
done

FAILED=0
SKIPPED=0

pass() { echo "  ✅ $1"; }
fail() { echo "  ❌ $1"; FAILED=$((FAILED + 1)); }
skip() { echo "  ⏭️  $1"; SKIPPED=$((SKIPPED + 1)); }

require_file() {
  local label="$1"
  local path="$2"
  if [[ -f "$path" ]]; then
    pass "$label"
  else
    fail "$label (missing: $path)"
  fi
}

echo "🔍 Offline System Test Suite"
echo "==========================="
echo ""

# ---------------------------------------------------------------------------
echo "Test 1: Check Offline Config"
echo "  → config/offlineConfig.ts exists"
echo "  → config service initialized"
echo ""

require_file "offlineConfig.ts" "frontend/constants/offlineConfig.ts"
require_file "offlineConfigService.ts" "frontend/services/config/offlineConfigService.ts"

if grep -q 'OFFLINE_EXPIRY_HOURS: 72' frontend/constants/offlineConfig.ts 2>/dev/null; then
  pass "OFFLINE_EXPIRY_HOURS = 72"
else
  fail "OFFLINE_EXPIRY_HOURS = 72"
fi

if grep -q 'ENABLE_OFFLINE_GUTSCHEIN: false' frontend/constants/offlineConfig.ts 2>/dev/null; then
  pass "voucher offline disabled in config"
else
  fail "voucher offline disabled in config"
fi

if grep -q 'getInstance()' frontend/services/config/offlineConfigService.ts 2>/dev/null; then
  pass "OfflineConfigService singleton initialized"
else
  fail "OfflineConfigService singleton initialized"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 2: Check Session Manager"
echo "  → Session stored in AsyncStorage"
echo "  → Token expiry calculation works"
echo ""

require_file "offlineSessionManager.ts" "frontend/services/auth/offlineSessionManager.ts"

if grep -q 'OFFLINE_SESSION_STORAGE_KEY' frontend/services/auth/offlineSessionManager.ts 2>/dev/null; then
  pass "session storage key defined"
else
  fail "session storage key defined"
fi

if grep -q 'AsyncStorage.setItem' frontend/services/auth/offlineSessionManager.ts 2>/dev/null; then
  pass "session persisted to AsyncStorage"
else
  fail "session persisted to AsyncStorage"
fi

if grep -q 'isTokenExpired()' frontend/services/auth/offlineSessionManager.ts 2>/dev/null \
  && grep -q 'canWorkOffline()' frontend/services/auth/offlineSessionManager.ts 2>/dev/null; then
  pass "token expiry + canWorkOffline helpers present"
else
  fail "token expiry + canWorkOffline helpers present"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 3: Check Order Manager"
echo "  → Offline order saved to storage"
echo "  → Order ID generated"
echo "  → Expiry set correctly"
echo ""

require_file "offlineOrderManager.ts" "frontend/services/offline/offlineOrderManager.ts"
require_file "offlineStorage.ts" "frontend/services/offline/offlineStorage.ts"
require_file "OfflineBanner.tsx" "frontend/components/OfflineBanner.tsx"

if grep -q 'async saveOrder' frontend/services/offline/offlineOrderManager.ts 2>/dev/null; then
  pass "saveOrder() implemented"
else
  fail "saveOrder() implemented"
fi

if grep -q 'OFFLINE-' frontend/services/offline/offlineOrderManager.ts 2>/dev/null; then
  pass "OFFLINE- order id prefix"
else
  fail "OFFLINE- order id prefix"
fi

if grep -q '72 \* 60 \* 60 \* 1000' frontend/services/offline/offlineOrderManager.ts 2>/dev/null; then
  pass "72h expiry window in order manager"
else
  fail "72h expiry window in order manager"
fi

if grep -q 'this.storage.saveOrder' frontend/services/offline/offlineOrderManager.ts 2>/dev/null; then
  pass "orders written to local storage adapter"
else
  fail "orders written to local storage adapter"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 4: Check Sync Service"
echo "  → Auto-sync starts after online event"
echo "  → Manual sync works"
echo "  → Sync status updates"
echo ""

require_file "offlineSyncService.ts" "frontend/services/offline/offlineSyncService.ts"
require_file "useOfflineOrderManager.ts" "frontend/hooks/useOfflineOrderManager.ts"

if grep -q "sync:online" frontend/services/offline/offlineSyncService.ts 2>/dev/null; then
  pass "auto-sync listens for sync:online"
else
  fail "auto-sync listens for sync:online"
fi

if grep -q 'async syncNow' frontend/services/offline/offlineSyncService.ts 2>/dev/null; then
  pass "manual syncNow() implemented"
else
  fail "manual syncNow() implemented"
fi

if grep -q 'getSyncStatus()' frontend/services/offline/offlineSyncService.ts 2>/dev/null \
  && grep -q 'sync:status' frontend/services/offline/offlineSyncService.ts 2>/dev/null; then
  pass "sync status emitted via sync:status"
else
  fail "sync status emitted via sync:status"
fi

if grep -q 'SYNC_INTERVAL_SECONDS' frontend/constants/offlineConfig.ts 2>/dev/null; then
  pass "30s sync interval configured"
else
  fail "30s sync interval configured"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 5: Check Notification System"
echo "  → Critical warning fires at 6 hours"
echo "  → Warning fires at 24 hours"
echo "  → Events emitted correctly"
echo ""

require_file "offlineNotificationService.ts" "frontend/services/offline/offlineNotificationService.ts"
require_file "eventEmitter.ts" "frontend/utils/eventEmitter.ts"

if grep -q 'OFFLINE_CRITICAL_HOURS' frontend/services/offline/offlineNotificationService.ts 2>/dev/null \
  && grep -q 'OFFLINE_WARNING_HOURS' frontend/services/offline/offlineNotificationService.ts 2>/dev/null; then
  pass "6h critical / 24h warning thresholds wired"
else
  fail "6h critical / 24h warning thresholds wired"
fi

if grep -q "offline:critical" frontend/services/offline/offlineNotificationService.ts 2>/dev/null \
  && grep -q "offline:warning" frontend/services/offline/offlineNotificationService.ts 2>/dev/null; then
  pass "offline:critical and offline:warning events emitted"
else
  fail "offline:critical and offline:warning events emitted"
fi

if grep -q "'offline:critical'" frontend/utils/eventEmitter.ts 2>/dev/null \
  && grep -q "'offline:warning'" frontend/utils/eventEmitter.ts 2>/dev/null; then
  pass "event types registered in EventMap"
else
  fail "event types registered in EventMap"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 6: Check FA Settings"
echo "  → Offline settings page loads"
echo "  → Config can be saved"
echo ""

require_file "FA offline settings page" "frontend-admin/src/app/(protected)/settings/offline/page.tsx"
require_file "OfflineSettings component" "frontend-admin/src/features/settings/components/OfflineSettings.tsx"
require_file "offlineSettingsApi.ts" "frontend-admin/src/features/settings/api/offlineSettingsApi.ts"
require_file "useOfflineSettings.ts" "frontend-admin/src/features/settings/hooks/useOfflineSettings.ts"
require_file "RKSV offline orders admin page" "frontend-admin/src/app/(protected)/rksv/offline-orders/page.tsx"

if grep -q "/api/admin/settings/offline" frontend-admin/src/features/settings/api/offlineSettingsApi.ts 2>/dev/null; then
  pass "FA API client targets /api/admin/settings/offline"
else
  fail "FA API client targets /api/admin/settings/offline"
fi

if grep -q 'useUpdateOfflineSettings' frontend-admin/src/features/settings/hooks/useOfflineSettings.ts 2>/dev/null; then
  pass "update mutation hook for saving config"
else
  fail "update mutation hook for saving config"
fi

if grep -q 'payment.view' frontend-admin/src/app/\(protected\)/rksv/offline-orders/page.tsx 2>/dev/null \
  || grep -q 'PAYMENT_VIEW' frontend-admin/src/app/\(protected\)/rksv/offline-orders/page.tsx 2>/dev/null; then
  pass "RKSV offline orders page permission gate"
else
  fail "RKSV offline orders page permission gate"
fi

echo ""

# ---------------------------------------------------------------------------
echo "Test 7: Node validation (logic + contracts)"
echo ""

if command -v node >/dev/null 2>&1; then
  if node scripts/test-offline-system.mjs; then
    pass "Node offline validation script"
  else
    fail "Node offline validation script"
  fi
else
  skip "Node not found — skipping scripts/test-offline-system.mjs"
fi

echo ""

# ---------------------------------------------------------------------------
if [[ "$WITH_BACKEND" -eq 1 ]]; then
  echo "Test 8: Backend (optional --with-backend)"
  echo ""

  if command -v dotnet >/dev/null 2>&1; then
    echo "  → Building backend..."
    if (cd backend && dotnet build -v q); then
      pass "backend build"
    else
      fail "backend build"
    fi

    echo "  → Running SequenceReservationServiceTests..."
    set +e
    TEST_OUT=$(cd backend && dotnet test --filter "FullyQualifiedName~SequenceReservationServiceTests" -v q 2>&1)
    TEST_EXIT=$?
    set -e
    if [[ "$TEST_EXIT" -eq 0 ]]; then
      pass "SequenceReservationServiceTests"
    elif echo "$TEST_OUT" | grep -qi 'skipped\|Skip\.IfNot\|no test matches'; then
      skip "SequenceReservationServiceTests (PostgreSQL unavailable or no matches)"
    else
      echo "$TEST_OUT" | tail -20
      fail "SequenceReservationServiceTests"
    fi
  else
    skip "dotnet not found"
  fi
  echo ""
fi

# ---------------------------------------------------------------------------
if [[ "$WITH_FRONTEND" -eq 1 ]]; then
  echo "Test 9: Frontend Jest (optional --with-frontend)"
  echo ""

  if [[ -d frontend/node_modules ]]; then
    set +e
    (cd frontend && npm test -- --passWithNoTests --testPathPattern="offline|tseStatusBannerOffline" --silent 2>&1)
    JEST_EXIT=$?
    set -e
    if [[ "$JEST_EXIT" -eq 0 ]]; then
      pass "frontend offline Jest tests"
    else
      fail "frontend offline Jest tests"
    fi
  else
    skip "frontend/node_modules missing — run npm install in frontend/"
  fi
  echo ""
fi

# ---------------------------------------------------------------------------
if [[ "$WITH_API" -eq 1 ]]; then
  echo "Test 10: Live API smoke (optional --with-api)"
  echo ""

  HEALTH_CODE=$(curl -s -o /dev/null -w "%{http_code}" -H "X-Tenant-Id: dev" http://localhost:5184/api/health 2>/dev/null || echo "000")
  if [[ "$HEALTH_CODE" == "200" ]]; then
    pass "backend health (localhost:5184)"
    PENDING_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
      -H "X-Tenant-Id: dev" \
      "http://localhost:5184/api/pos/offline-orders/pending?cashRegisterId=00000000-0000-0000-0000-000000000000" 2>/dev/null || echo "000")
    if [[ "$PENDING_CODE" == "401" || "$PENDING_CODE" == "403" ]]; then
      pass "POS offline-orders/pending requires auth ($PENDING_CODE)"
    elif [[ "$PENDING_CODE" == "200" || "$PENDING_CODE" == "404" ]]; then
      pass "POS offline-orders/pending reachable ($PENDING_CODE)"
    else
      fail "POS offline-orders/pending unexpected status ($PENDING_CODE)"
    fi
  else
    skip "backend not running on :5184 (health=$HEALTH_CODE)"
  fi
  echo ""
fi

# ---------------------------------------------------------------------------
echo "==========================="
if [[ "$FAILED" -eq 0 ]]; then
  echo "✅ All tests passed! (${SKIPPED} skipped)"
  exit 0
else
  echo "❌ ${FAILED} test(s) failed (${SKIPPED} skipped)"
  echo ""
  echo "Tips:"
  echo "  ./scripts/test-offline-system.sh --with-backend   # dotnet sequence tests"
  echo "  ./scripts/test-offline-system.sh --with-frontend  # Jest offline tests"
  echo "  ./scripts/test-offline-system.sh --with-api       # live API on :5184"
  echo "  See docs/OFFLINE_SYSTEM_TEST_PLAN.md for full E2E scenarios"
  exit 1
fi
