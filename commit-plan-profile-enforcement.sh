#!/usr/bin/env bash
# commit-plan-profile-enforcement.sh
# Split the POS profile-gate and profile-change lifecycle work into four commits.
# Each path is git-added once. This script does not add the earlier menu, page,
# or permission files; those stay in commit-plan-menu.sh.
#
#   DRY_RUN=1 bash commit-plan-profile-enforcement.sh   # print only (default)
#   DRY_RUN=0 bash commit-plan-profile-enforcement.sh   # create the commits
#
# Does not push.
#
# Run commit-plan-menu.sh first. These two paths stay in that script (Option A)
# because a whole-file add cannot split the menu edits from the impact edits:
#   frontend-admin/src/i18n/generated/translationKeys.ts
#     menu commit 5 (permission and page keys) and the impact keys from commit C.
#   frontend-admin/src/shared/__tests__/sidebarVerticalProfile.test.ts
#     menu commit 2 (profile filter) and the patient-menu assertion from commit C.
# This script prints a skip line for each and does not git-add them.
#
# docs/VERTICAL_PROFILES.md records the lifecycle and that a profile switch
# rejects new profile-specific writes. The 400/403 codes live in the commit A tests.

set -euo pipefail

DRY_RUN="${DRY_RUN:-1}"

run_git() {
  if [[ "${DRY_RUN}" == "1" ]]; then
    printf 'git'
    local arg
    for arg in "$@"; do
      printf ' %q' "$arg"
    done
    printf '\n'
    return 0
  fi
  git "$@"
}

if [[ -z "$(git status --porcelain)" ]]; then
  echo "preflight: working tree has no uncommitted changes." >&2
  exit 1
fi

# Orval rewrites frontend-admin/src/api/generated/** on `npm run generate:api`.
# Those files are not hand-edited, so whitespace there is not a review gate.
if ! git diff --check -- ':(exclude)frontend-admin/src/api/generated/**'; then
  echo "preflight: git diff --check reported conflict markers or whitespace errors (excluding generated)." >&2
  exit 1
fi

if ! git diff --cached --check -- ':(exclude)frontend-admin/src/api/generated/**'; then
  echo "preflight: staged diff failed git diff --check (excluding generated)." >&2
  exit 1
fi

if [[ -n "$(git ls-files -u)" ]]; then
  echo "preflight: unmerged paths are present." >&2
  exit 1
fi

echo "# unstage the index so each commit adds only its own paths"
run_git restore --staged :/

skip_owned_by_menu() {
  local path
  for path in "$@"; do
    echo "Skipping ${path} — already staged by commit-plan-menu.sh"
  done
}

# Commit A (23 files): reject profile-specific POS fields and endpoints.
run_git add -- \
  'backend/ApplicationHost.cs' \
  'backend/Controllers/PaymentController.cs' \
  'backend/Controllers/PosAppointmentsController.cs' \
  'backend/Controllers/PosCustomerController.cs' \
  'backend/Controllers/PosProductImeisController.cs' \
  'backend/Controllers/PosRoomsController.cs' \
  'backend/Controllers/PosTicketsController.cs' \
  'backend/Controllers/VerticalProfileGuardResponses.cs' \
  'backend/DTOs/PaymentDTOs.cs' \
  'backend/KasseAPI_Final.Tests/LodgingApiTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentPrescriptionTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentTaxiTests.cs' \
  'backend/KasseAPI_Final.Tests/PaymentTicketTests.cs' \
  'backend/KasseAPI_Final.Tests/PermissiveVerticalProfileGuard.cs' \
  'backend/KasseAPI_Final.Tests/PosAppointmentApiTests.cs' \
  'backend/KasseAPI_Final.Tests/PosCustomerVerticalProfileTests.cs' \
  'backend/KasseAPI_Final.Tests/PosTicketsApiTests.cs' \
  'backend/KasseAPI_Final.Tests/ProductImeiApiTests.cs' \
  'backend/KasseAPI_Final.Tests/VerticalProfileGuardTests.cs' \
  'backend/Services/PaymentService.cs' \
  'backend/Services/VerticalProfiles/FeatureNotEnabledForProfileException.cs' \
  'backend/Services/VerticalProfiles/IVerticalProfileGuard.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileGuard.cs'
run_git commit -m "$(cat <<'EOF'
feat(backend): enforce vertical profile features on POS endpoints

EOF
)"

# Commit B (4 files): profile-impact counts. Historical rows are not deleted.
run_git add -- \
  'backend/Controllers/AdminTenantVerticalProfileController.cs' \
  'backend/KasseAPI_Final.Tests/VerticalProfileLifecycleTests.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileDtos.cs' \
  'backend/Services/VerticalProfiles/VerticalProfileService.cs'
run_git commit -m "$(cat <<'EOF'
feat(backend): preserve historical data on profile change

EOF
)"

# Commit C (6 files): warning in the Super Admin profile editor.
# The two shared files stay in commit-plan-menu.sh.
skip_owned_by_menu \
  'frontend-admin/src/i18n/generated/translationKeys.ts' \
  'frontend-admin/src/shared/__tests__/sidebarVerticalProfile.test.ts'
run_git add -- \
  'frontend-admin/src/features/super-admin/components/TenantVerticalProfileEditor.tsx' \
  'frontend-admin/src/features/super-admin/components/__tests__/TenantVerticalProfileEditor.test.tsx' \
  'frontend-admin/src/features/super-admin/tenantProfileImpact.ts' \
  'frontend-admin/src/i18n/locales/de/tenants.json' \
  'frontend-admin/src/i18n/locales/en/tenants.json' \
  'frontend-admin/src/i18n/locales/tr/tenants.json'
run_git commit -m "$(cat <<'EOF'
feat(admin): show profile-change impact warning

EOF
)"

# Commit D (1 file): lifecycle section in the vertical-profile doc.
run_git add -- \
  'docs/VERTICAL_PROFILES.md'
run_git commit -m "$(cat <<'EOF'
docs: document profile feature enforcement and lifecycle

EOF
)"
