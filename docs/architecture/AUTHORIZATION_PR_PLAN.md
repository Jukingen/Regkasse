# Authorization Refactor — Incremental PR Plan

**Historical.** Legacy admin role has been fully removed. System uses **Admin** only (plus SuperAdmin). This plan is kept for reference.

**Goal:** Split authorization work into 4–6 low-risk PRs with clear rollback.  
**Context:** Canonical roles SuperAdmin, Admin, Manager, Cashier, Waiter; single admin role is Admin; SuperAdmin for system-critical.

---

## 1) PR sequence (4–6 PRs)

| # | Title | Scope | Risk | Test |
|---|--------|--------|------|------|
| **PR1** | docs: Authorization inventory and no-go checklist | Docs only: inventory, no-go list. No code change. | None | N/A |
| **PR2** | auth: Legacy role policies use Admin only | Policy lists use only SuperAdmin + Admin (and existing others). | Low | RolePermissionMatrix + UserManagementAuthorizationPolicyTests |
| **PR3** | auth: Middleware + token use Roles.* | Middleware and AuthController use Roles.*; Admin not blocked. | Low | Same + smoke payment endpoint as Admin |
| **PR4** | auth: Role constants and seed | Roles.cs / RoleSeedData / SQL script; seed uses Roles.* only. | Low | Seed + login as Admin → token has Admin |
| **PR5** | auth: Controller hard-coded role checks (CartController force-cleanup) | Force-cleanup uses Roles.Admin, Roles.Cashier; WaiterName comments. | Low | Force-cleanup as Admin and as Cashier |
| **PR6** | auth: Permission migration prep + regression tests | Permission migration doc, optional ReportsController Permission example, test alignment + new regression tests. | Low | Full auth test filter (RolePermissionMatrix, UserManagementAuthorizationPolicy, PermissionAuthorizationHandler, RoleCanonicalization) |

---

## 2) Per-PR detail

### PR1 — docs: Authorization inventory and no-go checklist

| Item | Detail |
|------|--------|
| **Files** | `docs/AUTHORIZATION_INVENTORY.md` (new or update), `docs/architecture/AUTHORIZATION_PR_PLAN.md` (this file). |
| **Scope** | Document current policies, roles, claim flow, seed, hard-coded risks. Add no-go checklist (Section 3 below). |
| **Behavior** | None. |
| **Rollback** | Revert doc commits. |

---

### PR2 — auth: Legacy role policies use Admin only

| Item | Detail |
|------|--------|
| **Files** | `backend/Authorization/AuthorizationExtensions.cs` (policy lists use only Roles.Admin, no legacy admin role). |
| **Scope** | Policy names unchanged. Role lists: AdminUsers, UsersView, UsersManage, BackofficeManagement, BackofficeSettings, PosSales, etc. |
| **Behavior** | Single admin role Admin; token carries Identity role (trim). |
| **Rollback** | Revert `AuthorizationExtensions.cs`. Re-run tests. |

---

### PR3 — auth: Middleware + token use Roles.*

| Item | Detail |
|------|--------|
| **Files** | `PaymentSecurityMiddleware.cs` (AllowedPaymentRoles = Roles.*). `AuthController.cs` (default role `Roles.Cashier`). |
| **Scope** | Middleware and controller use constants; Admin allowed. |
| **Behavior** | Admin and SuperAdmin not blocked on payment endpoints. |
| **Rollback** | Revert both files. |

---

### PR4 — auth: Role constants and seed

| Item | Detail |
|------|--------|
| **Files** | `Roles.cs`, `RoleSeedData.cs` (Roles.* only; no legacy admin seed), `CanonicalizeLegacyRoleNames.sql` (deprecated). |
| **Scope** | Seed uses Roles.*; single admin role Admin. |
| **Behavior** | Seed creates Admin, SuperAdmin, etc.; no legacy admin role. |
| **Rollback** | Revert file edits. |

---

### PR5 — auth: Controller hard-coded role checks (CartController force-cleanup)

| Item | Detail |
|------|--------|
| **Files** | `backend/Controllers/CartController.cs` (force-cleanup: `Roles.Admin`, `Roles.Cashier` + `Array.Exists`; WaiterName "Kasiyer" comments as display-only). `backend/Controllers/CartController.cs` add `using KasseAPI_Final.Authorization`. |
| **Scope** | Single endpoint: POST force-cleanup; display fallbacks unchanged. |
| **Behavior** | Cashier users now pass force-cleanup (previously "Kasiyer" blocked them). Admin unchanged. |
| **Rollback** | Revert CartController changes; restore previous role check (note: previous check was wrong for Cashier). |

---

### PR6 — auth: Permission migration prep + regression tests

| Item | Detail |
|------|--------|
| **Files** | `PERMISSION_MIGRATION_PREPARATION.md`, optional `ReportsController.cs` (`[HasPermission(AppPermissions.ReportView)]`). Tests: UserManagementAuthorizationPolicyTests, RolePermissionMatrixTests, RoleCanonicalizationTests (role trim/identity), AUTHORIZATION_REGRESSION_TEST_REPORT.md. |
| **Scope** | Docs + optional one controller permission example; test alignment and minimal new tests. |
| **Behavior** | ReportsController: ReportViewer can access reports if permission example applied. Tests document and guard refactor. |
| **Rollback** | Revert test and doc changes; revert ReportsController to `BackofficeManagement` if applied. |

---

## 3) No-go checklist

Before merging each PR (and before starting the next), confirm:

| Check | Description |
|-------|-------------|
| **Admin lockout** | Policy and middleware use Roles.Admin; token carries Identity role (trim). No lockout for Admin. |
| **Middleware** | PaymentSecurityMiddleware uses `Roles.Admin`, `Roles.SuperAdmin`, `Roles.Manager`, `Roles.Cashier`. |
| **Seed** | RoleSeedData seeds Roles.* only; single admin role Admin. CanonicalizeLegacyRoleNames.sql deprecated. |
| **Test pass** | Auth test filter (RolePermissionMatrix, UserManagementAuthorizationPolicy, PermissionAuthorizationHandler, RoleCanonicalization) passes. |
| **Smoke** | Login as Admin → token role "Admin"; UsersView and payment endpoint → 200/204. |

---

## 4) File reference (quick map)

| Area | Files |
|------|--------|
| Policies | `backend/Authorization/AuthorizationExtensions.cs` |
| Roles | `backend/Authorization/Roles.cs` |
| Matrix | `backend/Authorization/RolePermissionMatrix.cs` |
| Canonicalization | `backend/Auth/RoleCanonicalization.cs` |
| Token claims | `backend/Services/TokenClaimsService.cs` |
| Middleware | `backend/Middleware/PaymentSecurityMiddleware.cs` |
| Seed | `backend/Data/RoleSeedData.cs` |
| SQL script | `backend/Scripts/CanonicalizeLegacyRoleNames.sql` |
| Controllers | `backend/Controllers/AuthController.cs`, `CartController.cs`, `ReportsController.cs` (optional) |
| Tests | `backend/KasseAPI_Final.Tests/RolePermissionMatrixTests.cs`, `UserManagementAuthorizationPolicyTests.cs`, `PermissionAuthorizationHandlerTests.cs`, `RoleCanonicalizationTests.cs` |

Plan is short, actionable, and uses concrete repo paths; each PR has clear scope, risk, test, and rollback.
