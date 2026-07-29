#!/usr/bin/env bash
# Report deployment status to the API (optional). Used by CI after deploy/smoke/rollback.
#
# Env:
#   DEPLOYMENT_STATUS_URL   e.g. https://api.regkasse.at/api/webhooks/deployments/ci-report
#   DEPLOYMENT_STATUS_TOKEN bearer / X-Deploy-Token value (= Deployment:StatusReportToken)
#   STAGE                   staging|canary|production
#   STATUS                  pending|deploying|smoke_running|succeeded|failed|rolled_back
#   GIT_SHA, GIT_REF, IMAGE_TAG, PREVIOUS_IMAGE_TAG, TENANT_IDS, ERROR_MESSAGE, RUN_URL, TRIGGERED_BY
#   SMOKE_PASSED            true|false (optional)
#   SMOKE_SUMMARY           pipe-separated check results (optional)
#   SOAK_HOURS              optional canary soak hours for per-tenant history

set -euo pipefail

URL="${DEPLOYMENT_STATUS_URL:-}"
TOKEN="${DEPLOYMENT_STATUS_TOKEN:-}"

if [[ -z "${URL}" || -z "${TOKEN}" ]]; then
  echo "Deployment status webhook not configured — skipping report."
  exit 0
fi

STAGE="${STAGE:-staging}"
STATUS="${STATUS:-succeeded}"
GIT_SHA="${GIT_SHA:-}"
GIT_REF="${GIT_REF:-}"
IMAGE_TAG="${IMAGE_TAG:-}"
PREVIOUS_IMAGE_TAG="${PREVIOUS_IMAGE_TAG:-}"
TENANT_IDS="${TENANT_IDS:-}"
ERROR_MESSAGE="${ERROR_MESSAGE:-}"
RUN_URL="${RUN_URL:-}"
TRIGGERED_BY="${TRIGGERED_BY:-github-actions}"
SMOKE_PASSED="${SMOKE_PASSED:-}"
SMOKE_SUMMARY="${SMOKE_SUMMARY:-}"
SOAK_HOURS="${SOAK_HOURS:-}"

payload=$(python3 - <<'PY'
import json, os
smoke = os.environ.get("SMOKE_PASSED") or ""
smoke_passed = None
if smoke.lower() in ("true", "1", "yes"):
  smoke_passed = True
elif smoke.lower() in ("false", "0", "no"):
  smoke_passed = False
soak_raw = (os.environ.get("SOAK_HOURS") or "").strip()
soak_hours = int(soak_raw) if soak_raw.isdigit() else None
print(json.dumps({
  "stage": os.environ.get("STAGE", "staging"),
  "status": os.environ.get("STATUS", "succeeded"),
  "gitSha": os.environ.get("GIT_SHA") or None,
  "gitRef": os.environ.get("GIT_REF") or None,
  "imageTag": os.environ.get("IMAGE_TAG") or None,
  "previousImageTag": os.environ.get("PREVIOUS_IMAGE_TAG") or None,
  "tenantIds": [t.strip() for t in (os.environ.get("TENANT_IDS") or "").split(",") if t.strip()] or None,
  "errorMessage": os.environ.get("ERROR_MESSAGE") or None,
  "runUrl": os.environ.get("RUN_URL") or None,
  "triggeredBy": os.environ.get("TRIGGERED_BY") or "github-actions",
  "smokePassed": smoke_passed,
  "smokeSummary": os.environ.get("SMOKE_SUMMARY") or None,
  "soakHours": soak_hours,
}))
PY
)

curl -sS -X POST "${URL}" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Deploy-Token: ${TOKEN}" \
  --data "${payload}"
echo
echo "Reported status=${STATUS} stage=${STAGE}"
