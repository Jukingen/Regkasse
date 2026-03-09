# Legacy Admin Role — Full Removal Report

**Canonical auth model:** [FinalAuthorizationModel.md](FinalAuthorizationModel.md). This report documents the **completed** removal of the legacy admin role. The system uses only **Admin** (and SuperAdmin) as admin roles.

**Date:** 2025-03  
**Scope:** Legacy admin role removed entirely; single admin role is **Admin**. No backward compatibility (demo).

---

## 1) Where the legacy role was removed

### Backend (code)

| Location | Change |
|----------|--------|
| **Roles.cs** | No legacy constant. Canonical list: SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. |
| **RoleCanonicalization.cs** | No legacy mapping. `GetCanonicalRole` only trims/empty. |
| **TokenClaimsService.cs** | No Administrator logic; comment: "Role and permissions from Identity and RolePermissionMatrix". |
| **ITokenClaimsService.cs** | Comment: "Role and permissions from Identity and RolePermissionMatrix (Roles.* only; no legacy alias)." |
| **AuthorizationExtensions.cs** | Policies use only `Roles.Admin` (via RoleGroups); no Administrator. |
| **RolePermissionMatrix.cs** | No Administrator entry; only SuperAdmin and Admin have full permissions. |
| **RoleSeedData.cs** | No legacy admin seed; single admin role is Admin only. |
| **CanonicalizeLegacyRoleNames.sql** | Deprecated/no-op; kept for history only. |
| **UserManagementController.cs** | Uses `Roles.SuperAdmin` (not RoleCanonicalization.Canonical); `using KasseAPI_Final.Authorization`. |

### Backend (migration — unchanged)

- **Migrations (20260308140000, 20260309120000)**  
  Historical only. SQL uses the legacy DB value for one-time data migration/drop. No active role constant; do not change migration body (EF checksum).

### Tests

| File | Change |
|------|--------|
| **RoleCanonicalizationTests.cs** | No Administrator tests; only trim/identity, null/empty, unknown role. Uses `Roles.Admin`, `Roles.SuperAdmin`. |
| **RolePermissionMatrixTests.cs** | Matrix has Admin only; no legacy role. |
| **UserManagementAuthorizationPolicyTests.cs** | Admin/SuperAdmin pass. |
| **PermissionAuthorizationHandlerTests.cs** | Admin role passes permission policies. |

### Docs updated (no "Administrator" as current behavior)

- **AUTHORIZATION_DEMO_CLEANUP.md** — Rewritten; "legacy admin role" only, no role name.
- **PERMISSION_FIRST_ARCHITECTURE_DESIGN.md** — "SuperAdmin, Admin" (no Administrator).
- **AUTHORIZATION_REFACTOR_VERIFICATION_PR_REVIEW.md** — Historical note; tables and risks use Admin only.
- **PERMISSION_MIGRATION_PR_SUMMARY.md** — Admin only; test descriptions updated.
- **AUTHORIZATION_REGRESSION_TEST_REPORT.md** — Scope and test descriptions: Admin only.
- **PERMISSION_MIGRATION_PREPARATION.md** — RolePermissionMatrix and policies: Admin only.
- **ROLE_CONSTANTS_CLEANUP_REPORT.md** — Replaced with current state (Admin only).
- **AUTHORIZATION_NORMALIZATION_HARDENING_REPORT.md** — Admin login/smoke; no Administrator.
- **AUTHORIZATION_POLICY_CLEANUP_REPORT.md** — Historical note; policies use Admin only.
- **POS_AUTHORIZATION_REFACTOR_SUMMARY.md** — Admin only; smoke and rollback text updated.
- **AUTHORIZATION_INVENTORY.md** — Policy lists and inventory: Admin only; seed table and risk table updated.
- **AUTHORIZATION_PR_PLAN.md** — Historical note; PR descriptions and no-go: Admin only.

### Frontend (already done in previous refactor)

- **frontend-admin/roles.ts** — No Administrator in ROLES_* or ROLES_RKSV_MENU.
- **frontend-admin/AdminOnlyGate.tsx** — `role === 'Admin' || role === 'SuperAdmin'`.
- **frontend/ReportDisplay.tsx** — userRole type includes `'Admin'` (not Administrator).

---

## 2) Remaining references

- **Migrations only (historical):**  
  `backend/Migrations/20260308140000_CanonicalizeLegacyRoleNames.cs` and `20260309120000_DropAdministratorRole.cs`  
  The **SQL** uses the legacy role name as a **DB value** for one-time UPDATE/DELETE. This is not an active constant; changing the migration body would change the EF checksum. Comments state the role was removed and the single admin role is Admin.

- **Docs:** Some docs mention the removal or historical context; none present the legacy role as active.

**No** remaining references in:  
runtime code, policy definitions, RolePermissionMatrix, seed, TokenClaimsService, RoleCanonicalization, or tests. No hard-coded legacy role string in executable code.

---

## 3) Effect of full Admin-only transition

- **Login / token:** Identity role is whatever is in AspNetUserRoles (e.g. "Admin"). Token `role` claim is that value trimmed; no mapping. Users must have the **Admin** role (not Administrator) in Identity to get admin access.
- **Authorization:** All policies and middleware use `Roles.Admin` (and RoleGroups). Only the "Admin" role name is accepted for admin-level access.
- **Permission evaluation:** RolePermissionMatrix has an entry only for `Roles.Admin` (and SuperAdmin, Manager, etc.); no Administrator key. Permissions are resolved only for roles present in the matrix.
- **Seed:** New environments get the Admin role from RoleSeedData; legacy role is not created. Historical migrations move existing DB data to Admin and drop the legacy role row.
- **Frontend:** Admin and SuperAdmin are the only admin-style roles in role lists and gates.

---

## 4) What to verify in tests

- **RoleCanonicalizationTests:** `GetCanonicalRole("Admin")` returns `"Admin"`; null/empty return `""`; unknown role returns trimmed. No legacy role in tests.
- **RolePermissionMatrixTests:** `RoleHasPermission(Roles.Admin, …)`; matrix has Admin only; unknown role has no permissions.
- **UserManagementAuthorizationPolicyTests:** Principal with role `"Admin"` or `"SuperAdmin"` passes; Cashier denied.
- **PermissionAuthorizationHandlerTests:** Principal with role Admin passes permission policies.
- **Smoke (manual):** Login with Identity role **Admin** → token has `role: "Admin"`. Admin endpoints return 200/204.

Run:

```bash
cd backend
dotnet test --filter "FullyQualifiedName~RoleCanonicalizationTests|FullyQualifiedName~RolePermissionMatrixTests|FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~PermissionAuthorizationHandlerTests"
```

All targeted tests should pass.
