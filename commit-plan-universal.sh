#!/usr/bin/env bash
# commit-plan-universal.sh
# Third plan. Run after commit-plan-menu.sh and commit-plan-profile-enforcement.sh.
# Splits the remaining POS deep-link / onboarding / EAS / ticket-sales / public
# catalog / hook work into nine commits. Each path is git-added once.
#
#   DRY_RUN=1 bash commit-plan-universal.sh   # print git add / git commit only (default)
#   DRY_RUN=0 bash commit-plan-universal.sh   # create the commits
#
# Does not push.
#
# Inventory when this script was written: 61 porcelain paths.
# 58 of those paths are listed below. This file is commit H (59th add).
# Three Orval whitespace paths do not match a group and stay unstaged.
#
# Already committed (or absent) and not added:
#   frontend/app/index.tsx                         clean
#   frontend/services/linking/deepLinking.ts       clean; the POS change is the hook
#   frontend/services/deepLinking.ts               absent
#   frontend/contexts/OnboardingContext.tsx        absent; see posOnboarding.ts
#   frontend/assets/brands/**                      absent
#   TenantVerticalProfileEditor.tsx                clean (profile-enforcement commit C)
#   backend/Controllers/PublicTenantsController.cs clean
#   publicTenantProfileDto.ts                      clean; no generated-client diff
#
# Commit A reads verticalProfileId from the public tenant profile. Commit F
# adds that field on the C# DTO. The POS client types the field locally, so A
# typechecks before F. The industry line on login stays empty until F is applied.

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
  if [[ "${1:-}" == "commit" ]] && git diff --cached --quiet; then
    echo "skip: no staged changes for: git $*"
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

skip_already_committed() {
  local path
  for path in "$@"; do
    echo "Skipping ${path} — already committed or not in this working tree"
  done
}

# Expected by the plan outline, clean or absent. Do not git-add.
skip_already_committed \
  'frontend/app/index.tsx' \
  'frontend/services/deepLinking.ts' \
  'frontend/services/linking/deepLinking.ts' \
  'frontend/contexts/OnboardingContext.tsx' \
  'frontend/assets/brands' \
  'frontend-admin/src/features/super-admin/components/TenantVerticalProfileEditor.tsx' \
  'backend/Controllers/PublicTenantsController.cs' \
  'frontend-admin/src/api/generated/model/publicTenantProfileDto.ts'

echo "# left unstaged (no matching group): Orval trailing-whitespace hook"
echo "Unassigned frontend-admin/orval.config.ts"
echo "Unassigned frontend-admin/package.json"
echo "Unassigned frontend-admin/scripts/strip-generated-trailing-whitespace.mjs"

# Commit A (19 files): POS deep link, onboarding, and the login industry line.
run_git add -- \
  'frontend/__tests__/deepLinking.test.ts' \
  'frontend/__tests__/expoRouterStructure.contract.test.ts' \
  'frontend/__tests__/posOnboarding.test.tsx' \
  'frontend/app/(auth)/_layout.tsx' \
  'frontend/app/(auth)/login.tsx' \
  'frontend/app/(auth)/onboarding.tsx' \
  'frontend/app/_layout.tsx' \
  'frontend/app/tenant/[slug].tsx' \
  'frontend/contexts/AuthContext.tsx' \
  'frontend/hooks/useDeepLinkNavigation.ts' \
  'frontend/i18n/locales/de/auth.json' \
  'frontend/i18n/locales/de/verticalProfiles.json' \
  'frontend/i18n/locales/en/auth.json' \
  'frontend/i18n/locales/en/verticalProfiles.json' \
  'frontend/i18n/locales/tr/auth.json' \
  'frontend/i18n/locales/tr/verticalProfiles.json' \
  'frontend/services/linking/posTenantDeepLink.ts' \
  'frontend/services/verticalProfiles/posOnboarding.ts' \
  'frontend/services/verticalProfiles/posTenantBootstrap.ts'
run_git commit -m 'feat(pos): add deep link, onboarding, and universal app'

# Commit B (7 files): brand-aware EAS profiles and the workflow that builds them.
# frontend/assets/brands/** is absent. No icon drop is part of this commit.
run_git add -- \
  '.github/workflows/README.md' \
  '.github/workflows/frontend-eas-build.yml' \
  'frontend/.env.example' \
  'frontend/__tests__/appConfig.test.ts' \
  'frontend/app.config.js' \
  'frontend/eas.json' \
  'frontend/eslint.config.js'
run_git commit -m 'feat(eas): add brand-aware EAS build profiles'

# Commit C (12 files): hide profile surfaces unless the feature flag is on.
run_git add -- \
  'frontend/__tests__/ImeiPickerModal.test.tsx' \
  'frontend/__tests__/MobileServiceRoutePanel.test.tsx' \
  'frontend/__tests__/StaffPicker.test.tsx' \
  'frontend/__tests__/TableSelector.test.tsx' \
  'frontend/__tests__/TaxiSalePanel.test.tsx' \
  'frontend/__tests__/VetHairSalonScreens.test.tsx' \
  'frontend/app/(screens)/appointment.tsx' \
  'frontend/components/ImeiPickerModal.tsx' \
  'frontend/components/MobileServiceRoutePanel.tsx' \
  'frontend/components/StaffPicker.tsx' \
  'frontend/components/TableSelector.tsx' \
  'frontend/components/TaxiSalePanel.tsx'
run_git commit -m 'fix(pos): cleanup feature flags and component gates'

# Commit D (5 files): ticket-sales seed no longer enables room tracking.
run_git add -- \
  'backend/KasseAPI_Final.Tests/VerticalProfileDomainFieldsTests.cs' \
  'backend/Migrations/20261010150000_ClearTicketSalesRoomTracking.Designer.cs' \
  'backend/Migrations/20261010150000_ClearTicketSalesRoomTracking.cs' \
  'backend/Migrations/AppDbContextModelSnapshot.cs' \
  'backend/Models/VerticalProfile.cs'
run_git commit -m 'fix(backend): clear ticket-sales room tracking'

# Commit E (2 files): refresh the admin profile after a hub save.
# TenantVerticalProfileEditor.tsx is already committed and is not added.
run_git add -- \
  'frontend-admin/src/features/vertical-profiles/VerticalConfigHub.tsx' \
  'frontend-admin/src/features/vertical-profiles/__tests__/VerticalConfigHub.test.tsx'
run_git commit -m 'fix(admin): invalidate profile queries on save'

# Commit F (3 files): public tenant profile exposes verticalProfileId.
# The controller and the generated Orval DTO have no diff in this tree.
run_git add -- \
  'backend/DTOs/PublicTenantCatalogDtos.cs' \
  'backend/KasseAPI_Final.Tests/PublicTenantCatalogServiceTests.cs' \
  'backend/Services/Website/PublicTenantCatalogService.cs'
run_git commit -m 'feat(public): expose vertical profile on public tenant API'

# Commit G (2 files): pre-commit follows the staged path.
run_git add -- \
  'CONTRIBUTING.md' \
  'scripts/git-hooks/pre-commit.mjs'
run_git commit -m 'fix(hooks): stage-aware pre-commit checks'

# Commit H (3 files): the two earlier plan scripts plus this one.
run_git add -- \
  'commit-plan-menu.sh' \
  'commit-plan-profile-enforcement.sh' \
  'commit-plan-universal.sh'
run_git commit -m 'chore(scripts): add commit plan automation'

# Commit I (6 files): universal-app decision, deep links, and the EAS command notes.
run_git add -- \
  'DEPLOYMENT.md' \
  'docs/ANDROID_RELEASE_SIGNING.md' \
  'docs/OFFLINE_PRODUCTION_DEPLOYMENT.md' \
  'docs/POS_PRODUCTION_ARCHITECTURE.md' \
  'docs/VERTICAL_PROFILES.md' \
  'frontend/README.md'
run_git commit -m 'docs: document universal app decision and deep links'

echo "# done. DRY_RUN=${DRY_RUN}. No push."
echo "# counts: A 19, B 7, C 12, D 5, E 2, F 3, G 2, H 3, I 6 (59 paths). 3 Orval files left unstaged."
