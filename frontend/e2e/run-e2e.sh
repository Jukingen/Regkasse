#!/usr/bin/env bash
# Start the Development API with Fake TSE, then run POS Playwright.
# Does not edit appsettings. A manually started Fiskaly API is not reused.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
API_URL="${E2E_API_URL:-http://127.0.0.1:5184}"
PLAYWRIGHT_ARGS=("$@")
if [ "${#PLAYWRIGHT_ARGS[@]}" -eq 0 ]; then
  PLAYWRIGHT_ARGS=(critical-pos-flows)
fi

export ASPNETCORE_ENVIRONMENT=Development
export Tse__TseMode=Demo
export Tse__Mode=Fake
export Tse__Provider=fake
# TseSoftFallbackPolicy requires both flags in Development. Without them
# Mode=Fake still refuses to sign when Fiskaly credentials exist.
export Tse__FallbackEnabled=true
export Tse__SoftTseEnabled=true

if curl -fsS --max-time 2 "${API_URL}/api/health" >/dev/null 2>&1; then
  echo "Port 5184 is already serving an API. Stop that process first." >&2
  echo "Reusing it would keep Fiskaly for this run." >&2
  exit 1
fi

cd "$ROOT"
npm run dev:backend &
API_PID=$!
cleanup() {
  if kill -0 "$API_PID" >/dev/null 2>&1; then
    kill "$API_PID" >/dev/null 2>&1 || true
    wait "$API_PID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "Waiting for ${API_URL}/api/health ..."
for _ in $(seq 1 90); do
  if curl -fsS --max-time 2 -H "X-Tenant-Id: dev" "${API_URL}/api/health" >/dev/null 2>&1; then
    break
  fi
  if ! kill -0 "$API_PID" >/dev/null 2>&1; then
    echo "The API process exited before it became healthy." >&2
    exit 1
  fi
  sleep 2
done

if ! curl -fsS --max-time 2 -H "X-Tenant-Id: dev" "${API_URL}/api/health" >/dev/null 2>&1; then
  echo "The API did not become healthy in time." >&2
  exit 1
fi

cd "$ROOT/frontend"
npx playwright test "${PLAYWRIGHT_ARGS[@]}"
