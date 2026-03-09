# Authorization Demo Cleanup – Single Admin Role

**Date:** 2025-03  
**Scope:** Demo environment; backward compatibility not required.

## 1. Current state (before cleanup)

- Two admin-style roles existed: a legacy role and **Admin** (canonical).
- RoleCanonicalization mapped the legacy role → Admin at token time; policies used Admin only.
- RolePermissionMatrix and seed included the legacy role; one-time migration script existed.

## 2. Refactor plan

- Remove the legacy admin role everywhere: constant, seed, matrix, canonicalization, tests, FE/FE-Admin.
- Keep single admin role **Admin**; RoleCanonicalization is trim-only (no alias map).
- Use `Roles.*` (Authorization) as single source.

## 3. Files changed

| File | Change |
|------|--------|
| backend/Authorization/Roles.cs | Removed legacy admin constant; Canonical list uses Admin only. |
| backend/Authorization/RolePermissionMatrix.cs | Removed legacy matrix entry; Admin and SuperAdmin have full permissions. |
| backend/Authorization/AuthorizationExtensions.cs | Policies use Admin only (RoleGroups). |
| backend/Data/RoleSeedData.cs | No legacy admin seed; only Roles.* (Admin, SuperAdmin, …). |
| backend/Auth/RoleCanonicalization.cs | Trim-only; no LegacyToCanonical, GetLegacyAliases, or Canonical class. |
| backend/Controllers/UserManagementController.cs | Use Roles.SuperAdmin. |
| backend/Scripts/CanonicalizeLegacyRoleNames.sql | Deprecated / no-op; do not run. |
| backend/Migrations/20260308140000_CanonicalizeLegacyRoleNames.cs | Historical migration; comment updated. |
| backend/KasseAPI_Final.Tests/* | Legacy-role tests removed; RoleCanonicalization identity/trim only. |
| frontend-admin roles.ts, AdminOnlyGate, users page, types | Role lists use Admin only; AdminOnlyGate: Admin \|\| SuperAdmin. |
| frontend ReportDisplay.tsx | userRole type: Admin. |

## 4. FE impact

- **frontend-admin:** Role lists and RKSV menu use Admin only; AdminOnlyGate allows Admin or SuperAdmin.
- **frontend (POS):** ReportDisplay accepts `Admin` in userRole type.

## 5. Tests updated

- RoleCanonicalizationTests: identity, trim, null/empty, unknown role (no alias tests).
- RolePermissionMatrixTests: legacy-alias tests removed.
- UserManagementAuthorizationPolicyTests: Admin/SuperAdmin pass; no legacy-role test.
- PermissionAuthorizationHandlerTests: Admin permission test only (no legacy role).

## 6. Docs

- This file; PERMISSION_FIRST_ARCHITECTURE_DESIGN, AUTHORIZATION_REFACTOR_VERIFICATION_PR_REVIEW, PERMISSION_MIGRATION_PR_SUMMARY, AUTHORIZATION_REGRESSION_TEST_REPORT updated to reflect Admin-only.

## 7. Rollback note (demo)

Re-add legacy constant/seed/matrix/canonicalization and FE role arrays if ever needed; no backward-compatibility guarantee.

---

## Summary

- **Single admin role:** Admin (SuperAdmin for system-wide).
- **Canonical roles:** SuperAdmin, Admin, Manager, Cashier, Waiter.
- **Optional roles:** Kitchen, ReportViewer, Accountant.
- **Backend:** Roles.cs, RolePermissionMatrix, RoleSeedData, RoleCanonicalization, policies, token — all use Admin only; no legacy alias in code.
- **Frontend:** Role lists and gates use Admin (and SuperAdmin) only.
