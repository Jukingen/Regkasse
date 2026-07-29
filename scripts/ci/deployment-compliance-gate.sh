#!/usr/bin/env bash
# Production deployment compliance gate — fiscal checks + ComplianceOfficer sign-off.
#
# Env:
#   API_BASE                  Staging/canary API used for fiscal preflight (required)
#   IMAGE_TAG                 Image being promoted (required) — e.g. sha-abcdef1
#   STAGE                     production (default)
#   DEPLOYMENT_STATUS_URL     …/api/webhooks/deployments/ci-report (or host)
#   DEPLOYMENT_STATUS_TOKEN   Deployment:StatusReportToken
#   TENANT_ID                 Smoke tenant (default smoke)
#   OTHER_TENANT_ID           Foreign tenant for isolation check (default __no_such_tenant__)
#   LOGIN_IDENTIFIER / LOGIN_PASSWORD
#   REQUIRE_SIGNOFF=1         Require gatePassed from compliance API (default 1)
#   SKIP_FISCAL=0             Set 1 only for dry-runs (not production)
#
# Docs: docs/DEPLOYMENT_COMPLIANCE.md

set -euo pipefail

API_BASE="${API_BASE:-}"
IMAGE_TAG="${IMAGE_TAG:-}"
STAGE="${STAGE:-production}"
TENANT_ID="${TENANT_ID:-smoke}"
OTHER_TENANT_ID="${OTHER_TENANT_ID:-__no_such_tenant__}"
REQUIRE_SIGNOFF="${REQUIRE_SIGNOFF:-1}"
SKIP_FISCAL="${SKIP_FISCAL:-0}"
LOGIN_IDENTIFIER="${LOGIN_IDENTIFIER:-admin@admin.com}"
LOGIN_PASSWORD="${LOGIN_PASSWORD:-}"

if [[ -z "${API_BASE}" || -z "${IMAGE_TAG}" ]]; then
  echo "ERROR: API_BASE and IMAGE_TAG are required" >&2
  exit 2
fi
API_BASE="${API_BASE%/}"

TMP="${TMPDIR:-/tmp}/regkasse-compliance-$$"
mkdir -p "${TMP}"
cleanup() { rm -rf "${TMP}"; }
trap cleanup EXIT

fail=0
ok() { echo "OK  $1"; }
bad() { echo "FAIL $1${2:+ — $2}"; fail=1; }

http_code() {
  local url="$1"; shift
  curl -sS -o "${TMP}/body.json" -w "%{http_code}" --connect-timeout 10 --max-time 60 "$@" "${url}" || echo "000"
}

echo "Regkasse deployment compliance gate"
echo "===================================="
echo "API_BASE=${API_BASE}"
echo "IMAGE_TAG=${IMAGE_TAG}"
echo "STAGE=${STAGE}"
echo

# --- Compliance officer sign-off (API) ---
STATUS_URL="${DEPLOYMENT_STATUS_URL:-}"
TOKEN="${DEPLOYMENT_STATUS_TOKEN:-}"
if [[ "${REQUIRE_SIGNOFF}" == "1" ]]; then
  if [[ -z "${STATUS_URL}" || -z "${TOKEN}" ]]; then
    bad "compliance.signoff" "DEPLOYMENT_STATUS_URL/TOKEN required when REQUIRE_SIGNOFF=1"
  else
    GATE_URL="${STATUS_URL}"
    if [[ "${GATE_URL}" == *"/ci-report"* ]]; then
      GATE_URL="${GATE_URL%/ci-report}/compliance-gate"
    elif [[ "${GATE_URL}" != *"/compliance-gate"* ]]; then
      GATE_URL="${GATE_URL%/}/api/webhooks/deployments/compliance-gate"
    fi
    CODE=$(http_code "${GATE_URL}?imageTag=${IMAGE_TAG}&stage=${STAGE}" \
      -H "Authorization: Bearer ${TOKEN}" \
      -H "X-Deploy-Token: ${TOKEN}")
    PASSED=$(python3 -c "
import json,sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  print(0); sys.exit(0)
print(1 if d.get('gatePassed') else 0)
" "${TMP}/body.json")
    if [[ "${CODE}" == "200" && "${PASSED}" == "1" ]]; then
      ok "compliance.signoff"
    else
      bad "compliance.signoff" "HTTP ${CODE} gatePassed=${PASSED} (ComplianceOfficer must sign off in FA)"
    fi
  fi
else
  echo "SKIP compliance.signoff (REQUIRE_SIGNOFF=0)"
fi

if [[ "${SKIP_FISCAL}" == "1" ]]; then
  echo "SKIP fiscal preflight (SKIP_FISCAL=1)"
else
  CODE=$(http_code "${API_BASE}/api/Auth/login" \
    -X POST \
    -H "Content-Type: application/json" \
    -H "X-Tenant-Id: ${TENANT_ID}" \
    -d "{\"loginIdentifier\":\"${LOGIN_IDENTIFIER}\",\"password\":\"${LOGIN_PASSWORD}\",\"clientApp\":\"admin\"}")
  TOKEN_JWT=$(python3 -c "
import json,sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  print(''); sys.exit(0)
print(d.get('accessToken') or d.get('token') or '')
" "${TMP}/body.json")
  if [[ "${CODE}" == "200" && -n "${TOKEN_JWT}" ]]; then
    ok "fa.login"
  else
    bad "fa.login" "HTTP ${CODE}"
  fi

  if [[ -n "${TOKEN_JWT}" ]]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    set +e
    API_BASE="${API_BASE}" \
      TENANT_ID="${TENANT_ID}" \
      LOGIN_IDENTIFIER="${LOGIN_IDENTIFIER}" \
      LOGIN_PASSWORD="${LOGIN_PASSWORD}" \
      REQUIRE_READY=1 \
      REQUIRE_MIGRATIONS=0 \
      REQUIRE_DEP_EXPORT=1 \
      SMOKE_POS_PAYMENT=0 \
      SKIP_FA_UI=1 \
      SKIP_POS_UI=1 \
      bash "${SCRIPT_DIR}/../smoke-test.sh"
    smoke_rc=$?
    set -e
    if [[ "${smoke_rc}" -eq 0 ]]; then
      ok "fiscal.dep_export_smoke"
    else
      bad "fiscal.dep_export_smoke" "smoke-test.sh exit ${smoke_rc}"
    fi

    CODE=$(http_code "${API_BASE}/api/tse/health" \
      -H "Authorization: Bearer ${TOKEN_JWT}" \
      -H "X-Tenant-Id: ${TENANT_ID}")
    if [[ "${CODE}" == "200" ]]; then
      ok "tse.health"
    else
      bad "tse.health" "HTTP ${CODE}"
    fi

    CODE=$(http_code "${API_BASE}/health/finanzonline")
    if [[ "${CODE}" == "200" || "${CODE}" == "503" ]]; then
      ok "finanzonline.health"
    else
      bad "finanzonline.health" "HTTP ${CODE}"
    fi

    CODE=$(http_code "${API_BASE}/api/admin/system/time-sync" \
      -H "Authorization: Bearer ${TOKEN_JWT}" \
      -H "X-Tenant-Id: ${TENANT_ID}")
    if [[ "${CODE}" == "200" ]]; then
      SYNC=$(python3 -c "
import json,sys
try:
  d=json.load(open(sys.argv[1]))
except Exception:
  print(0); sys.exit(0)
print(1 if d.get('isSynchronized') else 0)
" "${TMP}/body.json")
      if [[ "${SYNC}" == "1" ]]; then
        ok "ntp.time_sync"
      else
        bad "ntp.time_sync" "isSynchronized=false"
      fi
    else
      CODE2=$(http_code "${API_BASE}/api/rksv/environment" \
        -H "Authorization: Bearer ${TOKEN_JWT}" \
        -H "X-Tenant-Id: ${TENANT_ID}")
      [[ "${CODE2}" == "200" ]] && ok "ntp.time_sync" || bad "ntp.time_sync" "HTTP ${CODE}/${CODE2}"
    fi

    CODE=$(http_code "${API_BASE}/api/admin/cash-registers" \
      -H "Authorization: Bearer ${TOKEN_JWT}" \
      -H "X-Tenant-Id: ${OTHER_TENANT_ID}")
    if [[ "${CODE}" == "404" || "${CODE}" == "400" || "${CODE}" == "401" || "${CODE}" == "403" ]]; then
      ok "tenant.isolation"
    else
      bad "tenant.isolation" "HTTP ${CODE} (expected 404 for unknown tenant)"
    fi
  fi
fi

echo
if [[ "${fail}" -ne 0 ]]; then
  echo "Compliance gate FAILED"
  exit 1
fi
echo "Compliance gate PASSED"
exit 0
