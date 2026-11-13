#!/usr/bin/env bash
# Production / stage rollback helper.
#
# Modes:
#   files (default) — restore package trees from backup/<stamp> (legacy)
#   docker          — redeploy previous GHCR/Docker image tag via webhook or docker compose
#
# Usage:
#   # Filesystem backup restore (existing behavior):
#   sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
#   sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh 20260719-120000
#
#   # Docker image rollback:
#   REGKASSE_ROLLBACK_CONFIRM=YES MODE=docker PREVIOUS_IMAGE=ghcr.io/org/regkasse-api:sha-abc1234 \
#     ./scripts/rollback-production.sh
#
# Env:
#   MODE=files|docker          default files
#   PREVIOUS_IMAGE             required for docker mode (full image ref)
#   ROLLBACK_WEBHOOK_URL       preferred for docker (POST JSON action=rollback)
#   DOCKER_COMPOSE_FILE        optional compose file for local docker pull+up
#   DOCKER_SERVICE             default api
#   REGKASSE_ROOT              default /var/www/regkasse (files mode)
#   SKIP_RESTART=1             files mode: restore only
#   SKIP_SMOKE=1               skip post-rollback smoke
#   API_BASE                   for smoke (default https://api.regkasse.at)
#   TENANT_ID                  smoke tenant (default smoke)
#   SLACK_WEBHOOK_URL / ONCALL_WEBHOOK_URL  notify on-call
#   STAGE                      staging|canary|production (notify + webhook metadata)
#
# Docs: docs/DEPLOYMENT_SMOKE_TEST.md · DEPLOYMENT.md

set -euo pipefail

MODE="${MODE:-files}"
STAGE="${STAGE:-production}"
SKIP_SMOKE="${SKIP_SMOKE:-0}"
SKIP_RESTART="${SKIP_RESTART:-0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

notify_oncall() {
  local status="$1"
  local detail="$2"
  local url="${ONCALL_WEBHOOK_URL:-${SLACK_WEBHOOK_URL:-}}"
  if [[ -z "${url}" ]]; then
    echo "Notify skipped (set ONCALL_WEBHOOK_URL or SLACK_WEBHOOK_URL)."
    return 0
  fi
  local text="[Regkasse rollback] stage=${STAGE} status=${status} ${detail}"
  curl -sS -X POST -H 'Content-Type: application/json' \
    --data "{\"text\":\"${text}\"}" \
    "${url}" >/dev/null || echo "WARN: on-call notify failed" >&2
  echo "On-call notified (${status})."
}

confirm_prompt() {
  if [[ "${REGKASSE_ROLLBACK_CONFIRM:-}" == "YES" ]]; then
    return 0
  fi
  read -r -p "Type YES to confirm rollback (${MODE}): " answer
  if [[ "${answer}" != "YES" ]]; then
    echo "Aborted."
    exit 1
  fi
}

run_smoke() {
  if [[ "${SKIP_SMOKE}" == "1" ]]; then
    echo "SKIP_SMOKE=1 — not running smoke."
    return 0
  fi
  local api="${API_BASE:-https://api.regkasse.at}"
  local tenant="${TENANT_ID:-smoke}"
  echo
  echo "Running post-rollback smoke against ${api} (tenant=${tenant})…"
  sleep 5
  if API_BASE="${api}" TENANT_ID="${tenant}" \
    REQUIRE_READY="${REQUIRE_READY:-1}" \
    REQUIRE_MIGRATIONS="${REQUIRE_MIGRATIONS:-0}" \
    REQUIRE_DEP_EXPORT="${REQUIRE_DEP_EXPORT:-0}" \
    SMOKE_POS_PAYMENT=0 \
    bash "${SCRIPT_DIR}/smoke-test.sh"; then
    echo "Post-rollback smoke PASSED."
    return 0
  fi
  echo "Post-rollback smoke FAILED." >&2
  return 1
}

rollback_docker() {
  local image="${PREVIOUS_IMAGE:-${1:-}}"
  if [[ -z "${image}" ]]; then
    echo "ERROR: PREVIOUS_IMAGE (or stamp arg as image) required for MODE=docker" >&2
    exit 2
  fi

  echo "Regkasse Docker image rollback"
  echo "==============================="
  echo "Stage: ${STAGE}"
  echo "Image: ${image}"
  echo
  confirm_prompt

  if [[ -n "${ROLLBACK_WEBHOOK_URL:-}" ]]; then
    curl -sS -X POST \
      -H 'Content-Type: application/json' \
      --data "{\"action\":\"rollback\",\"stage\":\"${STAGE}\",\"previousImage\":\"${image}\"}" \
      "${ROLLBACK_WEBHOOK_URL}"
    echo
    echo "Rollback webhook invoked."
  elif [[ -n "${DOCKER_COMPOSE_FILE:-}" ]]; then
    local svc="${DOCKER_SERVICE:-api}"
    echo "docker compose -f ${DOCKER_COMPOSE_FILE} pull/up ${svc} → ${image}"
    IMAGE="${image}" docker compose -f "${DOCKER_COMPOSE_FILE}" pull "${svc}"
    IMAGE="${image}" docker compose -f "${DOCKER_COMPOSE_FILE}" up -d "${svc}"
  else
    echo "ERROR: Set ROLLBACK_WEBHOOK_URL or DOCKER_COMPOSE_FILE for docker mode." >&2
    exit 2
  fi

  if run_smoke; then
    notify_oncall "ok" "image=${image}"
    echo "Docker rollback complete."
    exit 0
  fi
  notify_oncall "smoke_failed_after_rollback" "image=${image}"
  exit 1
}

rollback_files() {
  REGKASSE_ROOT="${REGKASSE_ROOT:-/var/www/regkasse}"
  BACKUP_ROOT="${BACKUP_ROOT:-${REGKASSE_ROOT}/backup}"

  STAMP="${1:-}"
  if [[ -z "${STAMP}" ]]; then
    if [[ ! -f "${BACKUP_ROOT}/LATEST" ]]; then
      echo "ERROR: No stamp argument and ${BACKUP_ROOT}/LATEST missing." >&2
      exit 1
    fi
    STAMP="$(tr -d '[:space:]' < "${BACKUP_ROOT}/LATEST")"
  fi

  SRC="${BACKUP_ROOT}/${STAMP}"
  if [[ ! -d "${SRC}" ]]; then
    echo "ERROR: Backup not found: ${SRC}" >&2
    echo "Available:" >&2
    ls -1 "${BACKUP_ROOT}" 2>/dev/null || true
    exit 1
  fi

  echo "Regkasse production rollback (files)"
  echo "===================================="
  echo "Stamp:  ${STAMP}"
  echo "Source: ${SRC}"
  echo "Target: ${REGKASSE_ROOT}"
  echo

  confirm_prompt

  rollback_component() {
    local name="$1"
    local src_dir="${SRC}/${name}"
    local dst_dir="${REGKASSE_ROOT}/${name}"

    if [[ ! -d "${src_dir}" ]]; then
      echo "SKIP ${name} (not in backup)"
      return 0
    fi

    mkdir -p "${dst_dir}"
    rsync -a --delete \
      --exclude 'appsettings.Production.json' \
      --exclude 'appsettings.Development.json' \
      --exclude '.env' \
      --exclude '.env.local' \
      --exclude '.env.production' \
      --exclude 'node_modules/' \
      "${src_dir}/" "${dst_dir}/"
    echo "OK restored ${name}"
  }

  rollback_component backend
  rollback_component frontend-admin
  rollback_component frontend

  if [[ "${SKIP_RESTART}" == "1" ]]; then
    echo
    echo "SKIP_RESTART=1 — not restarting services."
    notify_oncall "restored_no_restart" "stamp=${STAMP}"
    exit 0
  fi

  echo
  echo "Restarting services..."
  systemctl restart regkasse-api
  systemctl restart regkasse-fa || true
  systemctl restart regkasse-pos || true

  echo
  echo "Waiting for API health..."
  sleep 3
  if curl -fsS --connect-timeout 5 "http://127.0.0.1:5184/api/health" >/dev/null 2>&1 \
    || curl -fsS --connect-timeout 5 "http://127.0.0.1/api/health" >/dev/null 2>&1; then
    echo "OK health check"
  else
    echo "WARN: health check did not return 200 — inspect journalctl -u regkasse-api" >&2
    notify_oncall "health_failed" "stamp=${STAMP}"
    exit 1
  fi

  export API_BASE="${API_BASE:-http://127.0.0.1:5184}"
  if run_smoke; then
    notify_oncall "ok" "stamp=${STAMP}"
    echo "Rollback complete (stamp=${STAMP})."
    exit 0
  fi
  notify_oncall "smoke_failed_after_rollback" "stamp=${STAMP}"
  exit 1
}

case "${MODE}" in
  docker) rollback_docker "${1:-}" ;;
  files)  rollback_files "${1:-}" ;;
  *)
    echo "ERROR: MODE must be files or docker (got ${MODE})" >&2
    exit 2
    ;;
esac
