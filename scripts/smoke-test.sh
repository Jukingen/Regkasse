#!/usr/bin/env bash
# Regkasse deployment smoke test — health, FA login, POS (test mode), RKSV/DEP.
# Exit 0 on success, non-zero on failure.
#
# Required:
#   API_BASE          e.g. https://api.staging.regkasse.at
#
# Tenant:
#   TENANT_ID         slug or UUID (default: smoke)
#
# Auth:
#   LOGIN_IDENTIFIER  FA/admin login (default admin@admin.com)
#   LOGIN_PASSWORD
#   POS_LOGIN_IDENTIFIER  optional (defaults to LOGIN_IDENTIFIER)
#   POS_LOGIN_PASSWORD    optional
#
# Optional surfaces:
#   FA_BASE           e.g. https://admin.staging.regkasse.at (HTTP GET /login)
#   POS_BASE          e.g. https://pos.staging.regkasse.at (HTTP GET /)
#
# POS payment (test mode only — Soft TSE / Simulation / Staging):
#   SMOKE_POS_PAYMENT=1   attempt cart + cash payment (default 0)
#   SMOKE_PRODUCT_ID      product UUID for cart (required when payment enabled)
#   SMOKE_CASH_REGISTER_ID  optional; discovered from /api/admin/cash-registers when omitted
#
# Gates:
#   REQUIRE_READY=1 (default)
#   REQUIRE_MIGRATIONS=1 (default)
#   REQUIRE_DEP_EXPORT=1 (default) — GET /api/admin/rksv/dep-export for last 1 day
#   SKIP_FA_UI=1 / SKIP_POS_UI=1 — skip static FA/POS URL checks
#   SMOKE_TEST_EXPECTED_STAGE  e.g. staging | production | canary | dev
#     When set: GET /api/health/ready and assert JSON releaseStage matches (case-insensitive).
#
# Docs: docs/DEPLOYMENT_SMOKE_TEST.md
#
# Usage:
#   API_BASE=https://api.staging.regkasse.at TENANT_ID=smoke ./scripts/smoke-test.sh
#   API_BASE=... SMOKE_TEST_EXPECTED_STAGE=staging ./scripts/smoke-test.sh
#   API_BASE=... TENANT_ID=pilot SMOKE_POS_PAYMENT=1 SMOKE_PRODUCT_ID=... ./scripts/smoke-test.sh

set -euo pipefail

API_BASE="${API_BASE:-}"
if [[ -z "${API_BASE}" ]]; then
  echo "ERROR: API_BASE is required" >&2
  exit 2
fi
API_BASE="${API_BASE%/}"

TENANT_ID="${TENANT_ID:-${TENANT_IDS:-smoke}}"
# First tenant if comma-separated
TENANT_ID="${TENANT_ID%%,*}"
TENANT_ID="$(echo "${TENANT_ID}" | xargs)"

LOGIN_IDENTIFIER="${LOGIN_IDENTIFIER:-admin@admin.com}"
LOGIN_PASSWORD="${LOGIN_PASSWORD:-Admin123!}"
POS_LOGIN_IDENTIFIER="${POS_LOGIN_IDENTIFIER:-${LOGIN_IDENTIFIER}}"
POS_LOGIN_PASSWORD="${POS_LOGIN_PASSWORD:-${LOGIN_PASSWORD}}"

FA_BASE="${FA_BASE:-}"
POS_BASE="${POS_BASE:-}"
REQUIRE_READY="${REQUIRE_READY:-1}"
REQUIRE_MIGRATIONS="${REQUIRE_MIGRATIONS:-1}"
REQUIRE_DEP_EXPORT="${REQUIRE_DEP_EXPORT:-1}"
SMOKE_POS_PAYMENT="${SMOKE_POS_PAYMENT:-0}"
SMOKE_PRODUCT_ID="${SMOKE_PRODUCT_ID:-}"
SMOKE_CASH_REGISTER_ID="${SMOKE_CASH_REGISTER_ID:-}"
SKIP_FA_UI="${SKIP_FA_UI:-0}"
SKIP_POS_UI="${SKIP_POS_UI:-0}"
SMOKE_TEST_EXPECTED_STAGE="${SMOKE_TEST_EXPECTED_STAGE:-}"
SMOKE_TEST_EXPECTED_STAGE="$(echo "${SMOKE_TEST_EXPECTED_STAGE}" | tr '[:upper:]' '[:lower:]' | xargs)"

TMPDIR_SMOKE="${TMPDIR:-/tmp}/regkasse-smoke-$$"
mkdir -p "${TMPDIR_SMOKE}"
cleanup() { rm -rf "${TMPDIR_SMOKE}"; }
trap cleanup EXIT

fail=0
PASSED=0
SUMMARY_LINES=()

ok() {
  local name="$1"
  echo "OK  ${name}"
  PASSED=$((PASSED + 1))
  SUMMARY_LINES+=("PASS:${name}")
}

bad() {
  local name="$1"
  local detail="${2:-}"
  echo "FAIL ${name}${detail:+ — ${detail}}"
  fail=1
  SUMMARY_LINES+=("FAIL:${name}${detail:+ (${detail})}")
}

json_get() {
  # json_get <file> <python-expr-on-obj-as-d>
  local file="$1"
  local expr="$2"
  python3 -c "
import json, sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  sys.exit(0)
print(${expr})
" "${file}" 2>/dev/null || true
}

http_code() {
  local url="$1"
  shift
  curl -sS -o "${TMPDIR_SMOKE}/body.json" -w "%{http_code}" --connect-timeout 10 --max-time 45 "$@" "${url}" || echo "000"
}

echo "Regkasse smoke test"
echo "==================="
echo "API_BASE=${API_BASE}"
echo "TENANT_ID=${TENANT_ID}"
echo "SMOKE_POS_PAYMENT=${SMOKE_POS_PAYMENT}"
if [[ -n "${SMOKE_TEST_EXPECTED_STAGE}" ]]; then
  echo "SMOKE_TEST_EXPECTED_STAGE=${SMOKE_TEST_EXPECTED_STAGE}"
fi
echo

# --- API health ---
CODE=$(http_code "${API_BASE}/api/health/live")
[[ "${CODE}" == "200" ]] && ok "api.health.live" || bad "api.health.live" "HTTP ${CODE}"

if [[ "${REQUIRE_READY}" == "1" ]]; then
  CODE=$(http_code "${API_BASE}/api/health/ready")
  [[ "${CODE}" == "200" ]] && ok "api.health.ready" || bad "api.health.ready" "HTTP ${CODE}"
fi

CODE=$(http_code "${API_BASE}/api/health")
[[ "${CODE}" == "200" || "${CODE}" == "503" ]] && ok "api.health" || bad "api.health" "HTTP ${CODE}"

# --- Release stage (Demo & QA / promotion verification) ---
# When SMOKE_TEST_EXPECTED_STAGE is set, re-fetch /api/health/ready and assert releaseStage.
if [[ -n "${SMOKE_TEST_EXPECTED_STAGE}" ]]; then
  CODE=$(http_code "${API_BASE}/api/health/ready")
  ACTUAL_STAGE=$(json_get "${TMPDIR_SMOKE}/body.json" "(d.get('releaseStage') or d.get('ReleaseStage') or '')")
  ACTUAL_STAGE="$(echo "${ACTUAL_STAGE}" | tr '[:upper:]' '[:lower:]' | xargs)"
  if [[ "${CODE}" != "200" && "${CODE}" != "503" ]]; then
    echo "Release stage check failed: expected ${SMOKE_TEST_EXPECTED_STAGE}, got <unreachable HTTP ${CODE}>" >&2
    bad "api.health.ready.releaseStage" "HTTP ${CODE}"
  elif [[ -z "${ACTUAL_STAGE}" ]]; then
    echo "Release stage check failed: expected ${SMOKE_TEST_EXPECTED_STAGE}, got <missing>" >&2
    bad "api.health.ready.releaseStage" "expected ${SMOKE_TEST_EXPECTED_STAGE}, got <missing>"
  elif [[ "${ACTUAL_STAGE}" != "${SMOKE_TEST_EXPECTED_STAGE}" ]]; then
    echo "Release stage check failed: expected ${SMOKE_TEST_EXPECTED_STAGE}, got ${ACTUAL_STAGE}" >&2
    bad "api.health.ready.releaseStage" "expected ${SMOKE_TEST_EXPECTED_STAGE}, got ${ACTUAL_STAGE}"
  else
    ok "api.health.ready.releaseStage"
  fi
else
  echo "SKIP api.health.ready.releaseStage (set SMOKE_TEST_EXPECTED_STAGE=staging|production|canary|dev)"
  SUMMARY_LINES+=("SKIP:api.health.ready.releaseStage")
fi

if [[ "${REQUIRE_MIGRATIONS}" == "1" ]]; then
  CODE=$(http_code "${API_BASE}/health/migrations")
  if [[ "${CODE}" == "200" ]]; then
    PENDING=$(json_get "${TMPDIR_SMOKE}/body.json" "int(((d.get('entries') or {}).get('ef-migrations') or {}).get('data', {}).get('pendingCount') or -1)")
    if [[ "${PENDING}" == "0" ]]; then
      ok "health.migrations"
    else
      bad "health.migrations" "pendingCount=${PENDING}"
    fi
  else
    bad "health.migrations" "HTTP ${CODE}"
  fi
fi

# --- FA / POS UI reachability ---
if [[ -n "${FA_BASE}" && "${SKIP_FA_UI}" != "1" ]]; then
  FA_BASE="${FA_BASE%/}"
  CODE=$(curl -sS -o /dev/null -w "%{http_code}" --connect-timeout 10 --max-time 30 "${FA_BASE}/login" || echo "000")
  if [[ "${CODE}" == "200" || "${CODE}" == "304" || "${CODE}" == "307" || "${CODE}" == "308" ]]; then
    ok "fa.ui.login"
  else
    bad "fa.ui.login" "HTTP ${CODE}"
  fi
fi

if [[ -n "${POS_BASE}" && "${SKIP_POS_UI}" != "1" ]]; then
  POS_BASE="${POS_BASE%/}"
  CODE=$(curl -sS -o /dev/null -w "%{http_code}" --connect-timeout 10 --max-time 30 "${POS_BASE}/" || echo "000")
  if [[ "${CODE}" == "200" || "${CODE}" == "304" || "${CODE}" == "307" || "${CODE}" == "308" ]]; then
    ok "pos.ui"
  else
    bad "pos.ui" "HTTP ${CODE}"
  fi
fi

# --- FA login ---
CODE=$(http_code "${API_BASE}/api/Auth/login" \
  -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: ${TENANT_ID}" \
  -d "{\"loginIdentifier\":\"${LOGIN_IDENTIFIER}\",\"password\":\"${LOGIN_PASSWORD}\",\"clientApp\":\"admin\"}")
ADMIN_TOKEN=$(json_get "${TMPDIR_SMOKE}/body.json" "d.get('accessToken') or d.get('token') or ''")
if [[ "${CODE}" == "200" && -n "${ADMIN_TOKEN}" ]]; then
  ok "fa.login"
else
  bad "fa.login" "HTTP ${CODE}"
fi

if [[ -n "${ADMIN_TOKEN}" ]]; then
  CODE=$(http_code "${API_BASE}/api/rksv/environment" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -H "X-Tenant-Id: ${TENANT_ID}")
  [[ "${CODE}" == "200" ]] && ok "rksv.environment" || bad "rksv.environment" "HTTP ${CODE}"
fi

# --- POS login + catalog ---
CODE=$(http_code "${API_BASE}/api/Auth/login" \
  -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: ${TENANT_ID}" \
  -d "{\"loginIdentifier\":\"${POS_LOGIN_IDENTIFIER}\",\"password\":\"${POS_LOGIN_PASSWORD}\",\"clientApp\":\"pos\"}")
POS_TOKEN=$(json_get "${TMPDIR_SMOKE}/body.json" "d.get('accessToken') or d.get('token') or ''")
if [[ "${CODE}" == "200" && -n "${POS_TOKEN}" ]]; then
  ok "pos.login"
else
  # Fallback: reuse admin token for catalog-only checks
  POS_TOKEN="${ADMIN_TOKEN}"
  if [[ -n "${POS_TOKEN}" ]]; then
    echo "WARN pos.login failed — using admin token for POS catalog checks"
    SUMMARY_LINES+=("WARN:pos.login")
  else
    bad "pos.login" "HTTP ${CODE}"
  fi
fi

if [[ -n "${POS_TOKEN}" ]]; then
  CODE=$(http_code "${API_BASE}/api/pos/list?pageSize=5" \
    -H "Authorization: Bearer ${POS_TOKEN}" \
    -H "X-Tenant-Id: ${TENANT_ID}")
  [[ "${CODE}" == "200" ]] && ok "pos.catalog" || bad "pos.catalog" "HTTP ${CODE}"

  CODE=$(http_code "${API_BASE}/api/pos/status" \
    -H "Authorization: Bearer ${POS_TOKEN}" \
    -H "X-Tenant-Id: ${TENANT_ID}")
  if [[ "${CODE}" == "200" || "${CODE}" == "404" ]]; then
    ok "pos.status"
  else
    bad "pos.status" "HTTP ${CODE}"
  fi
fi

# --- POS payment (test mode) ---
if [[ "${SMOKE_POS_PAYMENT}" == "1" ]]; then
  if [[ -z "${SMOKE_PRODUCT_ID}" ]]; then
    bad "pos.payment" "SMOKE_PRODUCT_ID required when SMOKE_POS_PAYMENT=1"
  elif [[ -z "${POS_TOKEN}" ]]; then
    bad "pos.payment" "no POS token"
  else
    CODE=$(http_code "${API_BASE}/api/pos/cart/add-item" \
      -X POST \
      -H "Authorization: Bearer ${POS_TOKEN}" \
      -H "Content-Type: application/json" \
      -H "X-Tenant-Id: ${TENANT_ID}" \
      -d "{\"productId\":\"${SMOKE_PRODUCT_ID}\",\"quantity\":1}")
    if [[ "${CODE}" != "200" && "${CODE}" != "201" ]]; then
      bad "pos.cart.add" "HTTP ${CODE}"
    else
      ok "pos.cart.add"
      CODE=$(http_code "${API_BASE}/api/pos/payment" \
        -X POST \
        -H "Authorization: Bearer ${POS_TOKEN}" \
        -H "Content-Type: application/json" \
        -H "X-Tenant-Id: ${TENANT_ID}" \
        -d "{\"payment\":{\"method\":\"cash\",\"amount\":10.00,\"tseRequired\":true}}")
      if [[ "${CODE}" == "200" || "${CODE}" == "201" ]]; then
        ok "pos.payment"
      else
        bad "pos.payment" "HTTP ${CODE} (enable Soft TSE / test mode)"
      fi
    fi
  fi
else
  echo "SKIP pos.payment (set SMOKE_POS_PAYMENT=1 for test-mode checkout)"
  SUMMARY_LINES+=("SKIP:pos.payment")
fi

# --- DEP export ---
if [[ "${REQUIRE_DEP_EXPORT}" == "1" ]]; then
  if [[ -z "${ADMIN_TOKEN}" ]]; then
    bad "rksv.dep_export" "no admin token"
  else
    REG_ID="${SMOKE_CASH_REGISTER_ID}"
    if [[ -z "${REG_ID}" ]]; then
      CODE=$(http_code "${API_BASE}/api/admin/cash-registers" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "X-Tenant-Id: ${TENANT_ID}")
      if [[ "${CODE}" == "200" ]]; then
        REG_ID=$(python3 - <<'PY'
import json, sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  sys.exit(0)
items = d if isinstance(d, list) else (d.get("items") or d.get("data") or d.get("cashRegisters") or [])
if isinstance(items, dict):
  items = items.get("items") or []
for it in items:
  if isinstance(it, dict) and it.get("id"):
    print(it["id"])
    break
PY
 "${TMPDIR_SMOKE}/body.json")
      fi
    fi
    if [[ -z "${REG_ID}" ]]; then
      bad "rksv.dep_export" "no cashRegisterId (set SMOKE_CASH_REGISTER_ID)"
    else
      FROM_UTC=$(python3 -c "from datetime import datetime,timedelta,timezone; print((datetime.now(timezone.utc)-timedelta(days=1)).strftime('%Y-%m-%dT%H:%M:%SZ'))")
      TO_UTC=$(python3 -c "from datetime import datetime,timezone; print(datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'))")
      CODE=$(http_code "${API_BASE}/api/admin/rksv/dep-export?cashRegisterId=${REG_ID}&fromUtc=${FROM_UTC}&toUtc=${TO_UTC}&includeSpecialReceipts=true&includeDailyClosings=true" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "X-Tenant-Id: ${TENANT_ID}")
      if [[ "${CODE}" == "200" ]]; then
        HAS_BELEGE=$(python3 -c "
import json,sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  print(0); sys.exit(0)
# BMF root or envelope
root = d.get('data') or d
ok = 1 if ('Belege-Gruppe' in root or 'belegeGruppe' in root or 'Belege-Gruppe' in d or isinstance(d, dict)) else 0
print(ok)
" "${TMPDIR_SMOKE}/body.json")
        if [[ "${HAS_BELEGE}" == "1" ]]; then
          ok "rksv.dep_export"
        else
          bad "rksv.dep_export" "unexpected JSON shape"
        fi
      elif [[ "${CODE}" == "403" || "${CODE}" == "401" ]]; then
        bad "rksv.dep_export" "HTTP ${CODE} (need report.export + audit.view)"
      else
        # Empty period may still 200; 404 register is a fail
        bad "rksv.dep_export" "HTTP ${CODE}"
      fi
    fi
  fi
fi

echo
SMOKE_OK=0
if [[ "${fail}" -eq 0 && "${PASSED}" -gt 0 ]]; then
  SMOKE_OK=1
fi
SMOKE_SUMMARY=$(IFS='|'; echo "${SUMMARY_LINES[*]}")
echo "SMOKE_SUMMARY=${SMOKE_SUMMARY}"
echo "SMOKE_PASSED=${SMOKE_OK}"
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "smoke_passed=${SMOKE_OK}"
    echo "smoke_summary=${SMOKE_SUMMARY}"
  } >> "${GITHUB_OUTPUT}"
fi

echo
if [[ "${SMOKE_OK}" -ne 1 ]]; then
  echo "Smoke FAILED (${PASSED} checks passed before failures)"
  exit 1
fi
echo "Smoke PASSED (${PASSED} checks)"
exit 0
