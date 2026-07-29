#!/usr/bin/env bash
# Thin CI wrapper — delegates to scripts/smoke-test.sh (kept for older workflow refs).
# Prefer: bash scripts/smoke-test.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exec bash "${ROOT}/scripts/smoke-test.sh"
