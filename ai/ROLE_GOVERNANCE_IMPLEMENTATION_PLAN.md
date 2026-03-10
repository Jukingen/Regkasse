# Role Governance – Concrete Implementation Plan

**Date:** 2025-03-10  
**Scope:** This repository only. No code yet; plan only.  
**Target:** Final decision summary (8 system roles; normalization/migration; hard business rules).

**Already done (from previous work):**
- Backend: Delete guards (system role + userCount), role required on user create/update, CreateRole reserved name, 409 with userCount; `RoleWithPermissionsDto` has `canDelete`, `canEditPermissions`.
- Frontend: RoleManagementDrawer shows system/custom badges, sorted list, delete disabled + tooltip, helper text when userCount > 0, German display names.
- Migrations: Administrator → Admin already applied (`CanonicalizeLegacyRoleNames`, `DropAdministratorRole`).

---

## 1) Backend changes

| # | Location | Change | Notes |
|---|----------|--------|--------|
| 1.1 | `backend/Data/RoleSeedData.cs` | Seed only the 8 system roles. **Remove** creation of Kellner, Auditor, Demo, BranchManager. **Add** creation of Kitchen, ReportViewer, Accountant (they are in `Roles.Canonical` and `RolePermissionMatrix` but not currently in seed). Use `Roles.Kitchen`, `Roles.ReportViewer`, `Roles.Accountant` for consistency. | New environments get only canonical roles. Existing DB unchanged until data migration runs. |
| 1.2 | `backend/Services/PaymentService.cs` | Replace or extend `user?.Role == "Demo"` with `user?.IsDemo == true`. **Option A:** Check only `user?.IsDemo == true` (and remove Role == "Demo"). **Option B (transition):** Check `(user?.IsDemo == true) || (user?.Role == "Demo")` so existing Demo-role users still restricted until migration runs; later remove `Role == "Demo"`. | Three call sites: create payment, cancel payment, refund. Prefer Option B for safe rollout. |
| 1.3 | `backend/Authorization/Roles.cs` | No change. Already defines exactly the 8 system roles; `Roles.Canonical` is source of truth. | — |
| 1.4 | `backend/Authorization/RolePermissionMatrix.cs` | No change. Matrix already has the 8 roles. | — |
| 1.5 | `backend/Services/RoleManagementService.cs`, `Controllers/UserManagementController.cs` | No change. Delete guards, role required, reserved name, 409 body already implemented. | — |

**Summary:** Backend code changes are (1) RoleSeedData: remove 4 legacy, add 3 missing canonical; (2) PaymentService: use IsDemo (with optional backward compatibility for Role == "Demo").

---

## 2) Frontend changes

| # | Location | Change | Notes |
|---|----------|--------|--------|
| 2.1 | `frontend-admin/src/features/users/constants/copy.ts` | Ensure `ROLE_DISPLAY_NAMES` includes all 8 system roles (already has SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). No removal of display names. | Already aligned. |
| 2.2 | `frontend-admin/src/features/users/components/RoleManagementDrawer.tsx` | No change required for final decision. Already: system/custom badges, sort (system first), delete disabled when not deletable, tooltip and Alert for “reassign first”. | — |
| 2.3 | `frontend-admin/src/app/(protected)/users/page.tsx` | Role options come from `GET /api/UserManagement/roles` or roles from `useRolesWithPermissions`. After backend migration, legacy role names will not exist in DB, so dropdown will only show system + custom. Optional: filter GET /roles to “assignable” only (canonical + custom) if backend adds such filter. | No change required unless backend adds filtered endpoint. |
| 2.4 | Permission-first navigation | No change. Menus and routes remain permission-based; role is only source of permissions. | — |

**Summary:** Frontend is already aligned. Optional: document that after migration, legacy names disappear from API and thus from dropdowns.

---

## 3) Database / migration impact

| # | Type | Description | Tables |
|---|------|--------------|--------|
| 3.1 | **Data migration (new)** | Reassign users from legacy roles to target roles, then delete legacy role rows. Order: (1) Update `AspNetUsers.role` and `AspNetUserRoles` for Kellner → Waiter, BranchManager → Manager, Auditor → ReportViewer, Demo → Cashier; (2) For users migrated from Demo, set `AspNetUsers.is_demo = true`; (3) Delete from `AspNetUserRoles` where RoleId in (Kellner, BranchManager, Auditor, Demo); (4) Delete from `AspNetRoles` where Name in ('Kellner','BranchManager','Auditor','Demo'). | AspNetUsers, AspNetUserRoles, AspNetRoles |
| 3.2 | **Schema** | No new columns or tables. Only data changes and deletion of existing rows in AspNetRoles/AspNetUserRoles. | — |
| 3.3 | **Migration file(s)** | One or more EF Core data migrations (or SQL scripts run after deploy). Recommended: single migration class that runs SQL in order: reassign Kellner → Waiter, BranchManager → Manager, Auditor → ReportViewer, Demo → Cashier (+ set is_demo = true for Demo users), then remove AspNetUserRoles for those four roles, then DELETE AspNetRoles for those four names. Use parameterized/literal role names; handle missing role (idempotent where possible). | e.g. `backend/Migrations/YYYYMMDDHHMMSS_NormalizeLegacyRolesToCanonical.cs` |

**Critical:** Migration must **reassign before delete**. If a legacy role has 0 users, reassignment is a no-op; delete is still safe. If it has N users, reassignment updates N rows; only then delete.

**SQL outline (conceptual):**
- Get role IDs for Waiter, Manager, ReportViewer, Cashier (targets) and Kellner, BranchManager, Auditor, Demo (legacy).
- For each legacy role: UPDATE AspNetUsers SET role = '<target>' WHERE role = '<legacy>'; INSERT INTO AspNetUserRoles (UserId, RoleId) SELECT UserId, <targetRoleId> FROM AspNetUserRoles WHERE RoleId = <legacyRoleId> ON CONFLICT or equivalent; DELETE FROM AspNetUserRoles WHERE RoleId = <legacyRoleId>; (PostgreSQL: use proper joins to avoid duplicates.)
- For Demo → Cashier: also UPDATE AspNetUsers SET is_demo = true WHERE id IN (user ids that had Demo).
- Finally: DELETE FROM AspNetRoles WHERE Name IN ('Kellner','BranchManager','Auditor','Demo').

---

## 4) Seed changes

| # | File | Current | Target |
|---|------|---------|--------|
| 4.1 | `backend/Data/RoleSeedData.cs` | Seeds: Admin, Cashier, Kellner, Waiter, Auditor, Demo, Manager, BranchManager, SuperAdmin. Does **not** seed Kitchen, ReportViewer, Accountant. | Seed **only**: SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant (all 8 from `Roles.Canonical`). Remove all code blocks that create Kellner, Auditor, Demo, BranchManager. Add blocks for Kitchen, ReportViewer, Accountant (same pattern: `if (!await roleManager.RoleExistsAsync(Roles.Kitchen))` etc.). |
| 4.2 | Execution order | Seed runs on app startup (Program.cs). After deployment, new environments get 8 roles only. Existing environments already have AspNetRoles rows; migration will have removed the 4 legacy roles. Seed is idempotent (RoleExistsAsync), so adding Kitchen/ReportViewer/Accountant is safe for existing DBs that might not have them. | — |

**Note:** Existing DBs may already have Kitchen, ReportViewer, Accountant if they were created manually or by an older seed. Seed must remain idempotent.

---

## 5) API contract changes

| # | Endpoint / contract | Change | Notes |
|---|----------------------|--------|--------|
| 5.1 | `GET /api/UserManagement/roles` | **Optional:** Return only roles that are valid for assignment (canonical + custom). Today returns all AspNetRoles. After migration, legacy names are gone from DB, so response naturally shrinks. Explicit filter is optional. | No breaking change if not implemented. |
| 5.2 | `GET /api/UserManagement/roles/with-permissions` | No change. Already returns roleName, isSystemRole, userCount, canDelete, canEditPermissions. | — |
| 5.3 | `POST /api/UserManagement` (create user), `PUT /api/UserManagement/{id}` | No change. Role required; must exist. After migration, legacy role names no longer exist, so clients cannot assign them. | — |
| 5.4 | `DELETE /api/UserManagement/roles/{roleName}` | No change. Already 409 when userCount > 0; 400 for system role. | — |
| 5.5 | OpenAPI / generated client | Regenerate after any DTO change. RoleWithPermissionsDto already has canDelete, canEditPermissions. No new DTOs for this plan. | — |

**Summary:** No mandatory API contract change. Optional: document that only canonical + custom roles are returned or assignable after migration.

---

## 6) Safe rollout sequence

| Step | Action | Who/where | Rollback |
|------|--------|-----------|----------|
| 0 | **Backup** production DB. Export current user–role assignments (e.g. SELECT UserId, RoleId FROM AspNetUserRoles JOIN AspNetRoles). | Ops / DBA | Restore from backup. |
| 1 | **Deploy backend** with (a) PaymentService: restrict by `IsDemo` (and optionally keep `Role == "Demo"` for transition); (b) RoleSeedData **not** yet changed (or deploy seed change only to new instances). Prefer: deploy PaymentService IsDemo logic first (with Role == "Demo" fallback), so Demo users still restricted. | Backend deploy | Redeploy previous backend. |
| 2 | **Run data migration** (reassign Kellner→Waiter, BranchManager→Manager, Auditor→ReportViewer, Demo→Cashier + set IsDemo for ex-Demo users; then delete legacy role rows). Run in maintenance window. Verify: no user has legacy role; AspNetRoles has no Kellner, BranchManager, Auditor, Demo. | Migration script or EF migration | Restore DB from backup; redeploy previous backend. |
| 3 | **Deploy backend** with RoleSeedData cleanup (remove 4 legacy, add 3 canonical). New installs get 8 roles only; existing DB already cleaned by step 2. | Backend deploy | Redeploy; seed is idempotent, low risk. |
| 4 | **Deploy frontend** if any (no change required for final decision; already done). | Frontend deploy | — |
| 5 | **Optional:** Remove `Role == "Demo"` from PaymentService (rely only on IsDemo). Do after migration has run and no user has Role = Demo. | Backend | Revert commit. |

**Order constraint:** Data migration (step 2) must run before or with the seed cleanup (step 3). If migration runs first, DB has no legacy roles; then seed runs and does not create them (and adds Kitchen/ReportViewer/Accountant if missing).

---

## 7) Risks and unresolved decisions

| Risk | Mitigation |
|------|------------|
| **Users in legacy roles** | Migration reassigns them to target role. No delete of role until reassignment done. Document target mapping (Kellner→Waiter, etc.). |
| **Demo users** | Migration sets role to Cashier and IsDemo = true. PaymentService must restrict by IsDemo (and optionally Role == "Demo" until migration run). |
| **Migration fails mid-way** | Run in transaction where possible; have rollback plan (restore backup). Test on copy of production first. |
| **Unknown/typo roles in DB** | Data migration only touches Kellner, BranchManager, Auditor, Demo. Any other non-canonical role (e.g. "nedone") remains. Unresolved: run a separate “reassign unknown roles to Cashier” script or leave for manual admin. Recommend: document; optional follow-up migration or admin UI to list roles with userCount and suggest reassignment. |
| **GET /roles returns legacy names** | After migration they are gone. No change needed unless backend adds explicit filter. |
| **Frontend cache** | After migration, roles list refreshes on next load. No special cache invalidation required. |
| **Permission matrix** | Kellner, BranchManager, Auditor, Demo have no matrix entries; targets (Waiter, Manager, ReportViewer, Cashier) do. Reassignment increases permissions; no loss. |

**Unresolved decisions:**
- **Unknown roles:** Whether to add a one-off script or admin action to “reassign all users in role X to Cashier” for any role not in Canonical. Recommendation: document in runbook; optional admin-only endpoint or script.
- **Demo role removal timing:** Whether to remove `Role == "Demo"` from PaymentService in the same release as migration or in a follow-up (recommend: same release but after migration verified).
- **Seed on existing DB:** Whether to run seed after migration. Seed is idempotent; adding Kitchen, ReportViewer, Accountant is safe. Removing legacy from seed does not remove rows from DB (migration already did that).

---

## Checklist (implementation)

- [ ] Backend: RoleSeedData – remove Kellner, Auditor, Demo, BranchManager; add Kitchen, ReportViewer, Accountant.
- [ ] Backend: PaymentService – restrict by IsDemo; keep Role == "Demo" during transition or remove after migration.
- [ ] Backend: Add data migration (reassign 4 legacy → targets; set IsDemo for ex-Demo users; delete legacy AspNetUserRoles and AspNetRoles rows).
- [ ] Frontend: No code change required; optional doc/comment.
- [ ] Rollout: Backup → deploy backend (IsDemo + optional seed) → run migration → verify → optional remove Role == "Demo".
- [ ] Document: Runbook for migration; admin message for “reassign users first” when delete blocked.
