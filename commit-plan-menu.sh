#!/usr/bin/env bash
# commit-plan-menu.sh
# Split the vertical-profile menu work into the six review commits below.
# Run this script first, then commit-plan-profile-enforcement.sh.
# Each path is git-added once. Files that also contain another group's edits
# are marked OVERLAP and land in the earliest group that owns the file.
# translationKeys.ts and sidebarVerticalProfile.test.ts also contain
# profile-enforcement edits; this script owns both files (Option A).
#
#   DRY_RUN=1 bash commit-plan-menu.sh   # print git add / git commit only (default)
#   DRY_RUN=0 bash commit-plan-menu.sh   # create the commits
#
# Does not push.
#
# Typecheck note: groups 2, 3, and 5 share source. After commit 2 the context
# imports the simulation module (commit 3) and the sidebar registry references
# permission constants (commit 5). The tree typechecks once all six commits
# are applied, not at the intermediate boundaries.
#
# docs/VERTICAL_PROFILES.md is clean. Commit 6 does not add it. Write the
# menu-filtering section first if that file should be part of commit 6.

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

# Commit 1 (3 files): platform slug filter. No overlap.
run_git add -- \
  'backend/Controllers/AdminUsersController.cs' \
  'backend/Models/Constants/SystemTenantIds.cs' \
  'backend/KasseAPI_Final.Tests/AdminUsersControllerTests.cs'
run_git commit -m 'fix(admin): translate platform slug filter to SQL'

# Commit 2: vertical profile context and menu filters, plus the full Orval tree.
# Staging any path under api/generated/ runs full verify-api-client. That check
# regenerates the whole client and fails when sibling generated files stay
# unstaged, so this commit adds the directory instead of admin.ts alone.
# swagger.json stays here with the generated client.
# OVERLAP AdminVerticalProfileContext.tsx also subscribes to simulation (commit 3).
# OVERLAP adminSidebarRegistry.ts also adds the five management leaves (commit 4)
# and their permission constants (commit 5).
# OVERLAP sidebarVerticalProfile.test.ts also asserts those new paths (commit 4).
run_git add -- \
  'backend/swagger.json' \
  'frontend-admin/src/api/generated/' \
  'frontend-admin/src/app/(protected)/layout.tsx' \
  'frontend-admin/src/components/AdminSidebar.tsx' \
  'frontend-admin/src/components/CommandPalette/commandRegistry.ts' \
  'frontend-admin/src/features/users/components/RoleMenuPreviewPanel.tsx' \
  'frontend-admin/src/features/users/utils/roleMenuPreview.ts' \
  'frontend-admin/src/features/vertical-profiles/contexts/AdminVerticalProfileContext.tsx' \
  'frontend-admin/src/features/vertical-profiles/contexts/__tests__/AdminVerticalProfileContext.test.tsx' \
  'frontend-admin/src/hooks/useMenuSearchIndex.ts' \
  'frontend-admin/src/shared/__tests__/sidebarVerticalProfile.test.ts' \
  'frontend-admin/src/shared/adminSidebarRegistry.ts' \
  'frontend-admin/src/shared/auth/PermissionRouteGuard.tsx' \
  'frontend-admin/src/shared/auth/__tests__/PermissionRouteGuard.test.tsx' \
  'frontend-admin/src/shared/auth/__tests__/PermissionRouteGuard.verticalProfile.test.tsx' \
  'frontend-admin/src/shared/buildAdminSidebar.tsx' \
  'frontend-admin/src/shared/sidebarVerticalProfile.ts'
run_git commit -m 'feat(admin): add vertical profile context and menu filters'

# Commit 3 (8 files): Super Admin profile simulation (dropdown + localStorage).
run_git add -- \
  'frontend-admin/src/components/layout/Header.tsx' \
  'frontend-admin/src/features/vertical-profiles/__tests__/superAdminProfileSimulation.test.tsx' \
  'frontend-admin/src/features/vertical-profiles/components/SuperAdminProfileSimulationSelect.tsx' \
  'frontend-admin/src/features/vertical-profiles/superAdminProfileSimulation.ts' \
  'frontend-admin/src/features/vertical-profiles/verticalProfileCatalog.ts' \
  'frontend-admin/src/i18n/locales/de/admin-shell.json' \
  'frontend-admin/src/i18n/locales/en/admin-shell.json' \
  'frontend-admin/src/i18n/locales/tr/admin-shell.json'
run_git commit -m 'feat(admin): add Super Admin profile simulation'

# Commit 4 (33 files): profile-specific management pages and their admin APIs.
run_git add -- \
  'backend/Controllers/AdminAppointmentsController.cs' \
  'backend/Controllers/AdminProductImeiCatalogController.cs' \
  'backend/Controllers/AdminTaxiTripsController.cs' \
  'backend/Services/Imei/IProductImeiService.cs' \
  'backend/Services/Imei/ProductImeiDtos.cs' \
  'backend/Services/Imei/ProductImeiService.cs' \
  'frontend-admin/src/app/(protected)/admin/appointments/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/customer-addresses/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/imeis/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/patients/page.tsx' \
  'frontend-admin/src/app/(protected)/admin/taxi-trips/page.tsx' \
  'frontend-admin/src/features/appointments/AppointmentsBoard.tsx' \
  'frontend-admin/src/features/appointments/AppointmentsTable.tsx' \
  'frontend-admin/src/features/appointments/__tests__/AppointmentsTable.test.tsx' \
  'frontend-admin/src/features/appointments/api.ts' \
  'frontend-admin/src/features/customer-addresses/CustomerAddressesTable.tsx' \
  'frontend-admin/src/features/customer-addresses/__tests__/CustomerAddressesTable.test.tsx' \
  'frontend-admin/src/features/imeis/ImeiTable.tsx' \
  'frontend-admin/src/features/imeis/__tests__/ImeiTable.test.tsx' \
  'frontend-admin/src/features/imeis/api.ts' \
  'frontend-admin/src/features/patients/PatientsTable.tsx' \
  'frontend-admin/src/features/patients/__tests__/PatientsTable.test.tsx' \
  'frontend-admin/src/features/taxi-trips/TaxiTripsTable.tsx' \
  'frontend-admin/src/features/taxi-trips/__tests__/TaxiTripsTable.test.tsx' \
  'frontend-admin/src/features/taxi-trips/api.ts' \
  'frontend-admin/src/i18n/locales/de/admin.json' \
  'frontend-admin/src/i18n/locales/de/nav.json' \
  'frontend-admin/src/i18n/locales/en/admin.json' \
  'frontend-admin/src/i18n/locales/en/nav.json' \
  'frontend-admin/src/i18n/locales/tr/admin.json' \
  'frontend-admin/src/i18n/locales/tr/nav.json' \
  'frontend-admin/src/shared/auth/__tests__/profileManagementRoutes.test.tsx' \
  'frontend-admin/src/shared/auth/routePermissions.ts'
run_git commit -m 'feat(admin): add profile-specific management pages'

# Commit 5 (16 files): twelve profile permissions.
# OVERLAP translationKeys.ts is the generated union of commits 3, 4, and 5.
run_git add -- \
  'backend/Authorization/AppPermissions.cs' \
  'backend/Authorization/PermissionCatalog.cs' \
  'backend/Authorization/PermissionCatalogMetadata.cs' \
  'backend/Authorization/PermissionImplication.cs' \
  'backend/Authorization/RolePermissionMatrix.cs' \
  'backend/KasseAPI_Final.Tests/RolePermissionMatrixTests.cs' \
  'docs/PERMISSIONS_MATRIX.md' \
  'frontend-admin/src/i18n/generated/translationKeys.ts' \
  'frontend-admin/src/i18n/locales/de/access.json' \
  'frontend-admin/src/i18n/locales/de/users.json' \
  'frontend-admin/src/i18n/locales/en/access.json' \
  'frontend-admin/src/i18n/locales/en/users.json' \
  'frontend-admin/src/i18n/locales/tr/access.json' \
  'frontend-admin/src/i18n/locales/tr/users.json' \
  'frontend-admin/src/shared/auth/permissionImplication.ts' \
  'frontend-admin/src/shared/auth/permissions.ts'
run_git commit -m 'feat(auth): add 12 profile-specific permissions'

# Commit 6 (14 files): sidebar screenshots from the browser smoke test.
# docs/VERTICAL_PROFILES.md is unchanged and is not included.
run_git add -- \
  'docs/images/vertical-profile-sidebars/actual-tenant.png' \
  'docs/images/vertical-profile-sidebars/all-profiles.png' \
  'docs/images/vertical-profile-sidebars/beherbergung.png' \
  'docs/images/vertical-profile-sidebars/gastronomy-tables.png' \
  'docs/images/vertical-profile-sidebars/gastronomy.png' \
  'docs/images/vertical-profile-sidebars/hair-salon.png' \
  'docs/images/vertical-profile-sidebars/handy-shop.png' \
  'docs/images/vertical-profile-sidebars/header-profile-simulation.png' \
  'docs/images/vertical-profile-sidebars/manager-gastronomy.png' \
  'docs/images/vertical-profile-sidebars/manager-header.png' \
  'docs/images/vertical-profile-sidebars/mobile-services.png' \
  'docs/images/vertical-profile-sidebars/taxi.png' \
  'docs/images/vertical-profile-sidebars/ticket-sales.png' \
  'docs/images/vertical-profile-sidebars/vet.png'
run_git commit -m 'docs: document vertical profile menu filtering'

echo "# done. DRY_RUN=${DRY_RUN}. No push."
