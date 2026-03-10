# Target Authorization and Role-Management Model

**Date:** 2025-03-10  
**Context:** Builds on `ROLE_TAXONOMY_ANALYSIS.md`; simplified POS role taxonomy and lifecycle.

---

## Pre-implementation summary

### 1) Domain decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **System roles** are exactly the 8 canonical roles (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). Defined in code only; permissions from `RolePermissionMatrix`. | Single source of truth; no DB-driven system role set. |
| D2 | **Custom roles** are any role in AspNetRoles not in the canonical list. Permissions stored in AspNetRoleClaims; create/update/delete via API. | Flexibility for tenants without touching code. |
| D3 | **System roles have fixed permissions.** No API or UI to change system role permissions; changes require code + release. | Prevents accidental weakening of Admin/SuperAdmin and keeps audit trail predictable. |
| D4 | **A role with assigned users cannot be deleted.** Delete returns a clear domain error (409 + code + message). Reassignment is a separate step (user update) before delete. | No orphaned users; explicit reassignment flow. |
| D5 | **Users must never be role-less.** Create and update user require a valid, assignable role; removing the last role is disallowed (single-role model: “change role” only). | Authorization invariant. |
| D6 | **Archive is not in scope.** Only hard delete for custom roles with zero users. Soft-delete/archive can be added later if needed. | Keeps first version simple. |
| D7 | **Assignable roles** = system roles + custom roles (roles that exist in AspNetRoles). Legacy names (Kellner, etc.) are removed by migration; GET /roles can be restricted to assignable-only. | Clean dropdown and consistent behavior. |
| D8 | **Demo** is not a system role. Restrict “demo” behavior via user flag `IsDemo` (and/or environment). PaymentService checks `IsDemo` (with optional backward compatibility for role "Demo" during migration). | Aligns with taxonomy analysis. |

---

### 2) Affected backend and frontend areas

| Area | Files / components |
|------|--------------------|
| **Backend – role definition** | `Authorization/Roles.cs`, `Authorization/RolePermissionMatrix.cs`, `Data/RoleSeedData.cs` |
| **Backend – role service** | `Services/IRoleManagementService.cs`, `Services/RoleManagementService.cs`, `Services/RolePermissionResolver.cs` |
| **Backend – role API** | `Controllers/UserManagementController.cs` (GET/POST roles, GET with-permissions, PUT permissions, DELETE role) |
| **Backend – user API** | `Controllers/UserManagementController.cs` (Create user, Update user – role required, role validation) |
| **Backend – auth** | `Services/TokenClaimsService.cs`, `Authorization/PermissionAuthorizationHandler.cs`, `Auth/RoleCanonicalization.cs` |
| **Frontend – role list / metadata** | `features/users/api/usersGateway.ts`, `api/generated/user-management` (types), `features/users/hooks/useRolesWithPermissions.ts` |
| **Frontend – role management UI** | `features/users/components/RoleManagementDrawer.tsx` (isSystemRole, userCount, delete/save disabled states) |
| **Frontend – user form** | `features/users/components/UserFormDrawer.tsx`, `app/(protected)/users/page.tsx` (roleOptions, role required) |
| **Frontend – role constants** | `features/auth/constants/roles.ts`, `shared/auth/permissions.ts` |

---

### 3) Delete / reassign / role-protection strategy

- **Delete**
  - **System role:** Not allowed. API returns 400 with `SYSTEM_ROLE_NOT_DELETABLE`; UI never shows delete for system roles.
  - **Custom role with users:** Not allowed. API returns 409 with `ROLE_HAS_ASSIGNED_USERS` and a clear message (e.g. “Cannot delete role: one or more users are assigned to this role.”). Optionally include `userCount` in response for UI.
  - **Custom role with zero users:** Allowed. DELETE succeeds; audit log entry.

- **Reassignment**
  - There is no “reassign all users from role A to B” API in scope. Reassignment is done by editing each user (PUT user with new role). Before deleting a custom role, admin must change every assigned user to another role; then delete the role.
  - Optional future: bulk “replace role” endpoint (e.g. “reassign all users of role X to role Y”) to simplify cleanup.

- **Protection**
  - **System roles:** Identified by name in `Roles.Canonical`. Not deletable; permissions not editable via API. Backend enforces in `RoleManagementService` (delete + set permissions). Frontend uses `isSystemRole` to disable delete and permission checkboxes/save.

---

## 1) Recommended domain model

### Role (conceptual; storage = AspNetRoles + optional AspNetRoleClaims)

| Attribute | Type | Source | Meaning |
|-----------|------|--------|---------|
| **Name** | string | AspNetRoles.Name | Unique role identifier. |
| **isSystemRole** | bool | Derived (name ∈ Roles.Canonical) | True iff role is one of the 8 system roles. |
| **permissions** | string[] | RolePermissionMatrix (system) or AspNetRoleClaims (custom) | Effective permission keys. |
| **userCount** | int | Computed (count users in role) | Number of users assigned. Used for delete guard and UI. |
| **isDeletable** | bool | Derived: !isSystemRole && userCount == 0 | True only for custom roles with no users. |
| **canEditPermissions** | bool | Derived: !isSystemRole | True only for custom roles. |

No separate “archive” state in this design; role either exists (and is assignable) or is deleted.

### User–role relationship

- **Single-role model:** Each user has exactly one role (ApplicationUser.Role + single entry in AspNetUserRoles). Invariant: after create/update, user has exactly one valid role.

### Role metadata (API and UI)

Expose in GET “roles with permissions” (and optionally in GET “roles” if that endpoint is extended):

- `roleName`, `permissions`, `isSystemRole`, `userCount`
- Derived on backend or client: `isDeletable` = !isSystemRole && userCount === 0, `canEditPermissions` = !isSystemRole

---

## 2) Hard business rules

| ID | Rule | Enforcement point |
|----|------|--------------------|
| R1 | System roles are not deletable. | Backend: RoleManagementService.DeleteRoleAsync → DeleteRoleResult.SystemRoleNotDeletable. |
| R2 | A role with at least one assigned user is not deletable. | Backend: RoleManagementService.DeleteRoleAsync → DeleteRoleResult.RoleHasAssignedUsers; return 409. |
| R3 | System role permissions are not editable via API. | Backend: RoleManagementService.SetRolePermissionsAsync → SetRolePermissionsResult.SystemRoleNotEditable. |
| R4 | Every user has a valid role after create and after update. | Backend: Create user – role required and must exist; Update user – if role is changed, new role required and must exist; disallow “clear role”. |
| R5 | Only existing roles can be assigned. | Backend: Create/Update user – role must exist in AspNetRoles (and optionally be in assignable list). |
| R6 | Custom role name must not duplicate a system role name (case-insensitive). | Backend: CreateRole – reject if request.Name matches any Roles.Canonical. |
| R7 | Deleting a role returns a clear domain error when userCount > 0. | Backend: 409, code ROLE_HAS_ASSIGNED_USERS, message including that users are assigned (optionally userCount). |

---

## 3) API contract implications

| Endpoint | Current / change | Contract |
|----------|------------------|----------|
| **GET /api/UserManagement/roles** | Optional: return only assignable roles, or all with metadata. | If unchanged: list of role names. If extended: e.g. `{ name, isSystemRole, userCount }[]` so UI can derive isDeletable/canEditPermissions without calling with-permissions for every role. |
| **GET /api/UserManagement/roles/with-permissions** | Already returns RoleWithPermissionsDto (roleName, permissions, isSystemRole, userCount). | Add nothing required; optional: add `isDeletable`, `canEditPermissions` for convenience. |
| **PUT /api/UserManagement/roles/{roleName}/permissions** | No change. | 400/403 for system role (SystemRoleNotEditable); 400 for invalid keys. |
| **DELETE /api/UserManagement/roles/{roleName}** | No change. | 400 for system role (code SYSTEM_ROLE_NOT_DELETABLE); 409 for custom role with users (code ROLE_HAS_ASSIGNED_USERS, message clear). Optional: include `userCount` in 409 body. |
| **POST /api/UserManagement/roles** | Add validation: name must not match any system role (case-insensitive). | 400 if name is a system role (code ROLE_NAME_RESERVED or similar). |
| **POST /api/UserManagement** (create user) | Require role in request; validate role exists (and optionally is assignable). | 400 if role missing or invalid. |
| **PUT /api/UserManagement/{id}** (update user) | If role is sent, it must be valid; do not allow clearing role (empty string) when user currently has one. | 400 if role empty or invalid. |

Error response shape (align with existing):

- `{ message: string, code: string }` and optionally `errors`, `userCount` for 409.

---

## 4) Frontend UX implications

| Topic | Behavior |
|-------|----------|
| **Protected (system) roles** | Show in role list with a “System” badge or label. Disable “Delete” and “Save” (permissions); disable permission checkboxes. Tooltip on Delete: e.g. “System roles cannot be deleted.” |
| **Custom role with users** | Delete button enabled (user can click). On click: show modal “This role has N users. Reassign them to another role before deleting.” and do not call DELETE; or call DELETE and show API 409 message. Prefer blocking in UI when userCount > 0 and show clear message + optional “Reassign users” link to user list filtered by role. |
| **Custom role with zero users** | Delete enabled; confirm modal; then DELETE. |
| **Role dropdown (user create/edit)** | Only assignable roles (system + custom). Options from GET roles or GET with-permissions; filter to assignable. Role required in form; validation before submit. |
| **Permission-first menu** | No change. Menu visibility remains permission-based; role is only the source of permissions. Token/claims still carry permissions (or role); handler uses permission checks. |
| **Metadata** | Use isSystemRole to disable edit/delete. Use userCount to show “N users” and to block delete (and show reassignment hint). Optional: show isDeletable/canEditPermissions if API provides them. |

---

## 5) Backend enforcement plan

| Rule | Where | How |
|------|--------|-----|
| System role not deletable | RoleManagementService.DeleteRoleAsync | Already: IsSystemRole(roleName) → return DeleteRoleResult.SystemRoleNotDeletable. |
| Role with users not deletable | RoleManagementService.DeleteRoleAsync | Already: GetUsersInRoleAsync; if Count > 0 return DeleteRoleResult.RoleHasAssignedUsers. |
| 409 and message for “users assigned” | UserManagementController.DeleteRole | Already: 409, code ROLE_HAS_ASSIGNED_USERS, message. Optional: add userCount in response body. |
| System role permissions not editable | RoleManagementService.SetRolePermissionsAsync | Already: IsSystemRole → SetRolePermissionsResult.SystemRoleNotEditable. |
| User must have role (create) | UserManagementController create user | Add: require request.Role non-empty; validate role exists (FindByNameAsync); 400 if missing or not found. |
| User must have role (update) | UserManagementController update user | Add: if request.Role is provided and empty, 400 (cannot clear role). If provided non-empty, validate role exists; 400 if not found. |
| Create custom role: name not system | UserManagementController CreateRole | Add: if Roles.Canonical.Contains(request.Name) (case-insensitive), return 400 ROLE_NAME_RESERVED. |
| Optional: GET /roles assignable-only | UserManagementController GetRoles | Filter to Roles.Canonical ∪ custom roles (e.g. roles that exist and are either in Canonical or have been created via API). Or keep returning all AspNetRoles and filter in UI. |

Token/claims: no change. Token still gets role(s) and optionally permissions from RolePermissionResolver; PermissionAuthorizationHandler uses permission claims or role→permission resolution. System vs custom only affects management (delete/edit permissions), not token issuance.

---

## 6) Edge cases

| Edge case | Handling |
|-----------|----------|
| **Last user in role:** Admin deletes user instead of reassigning. | Allowed. User delete is separate from role delete. Role becomes deletable when userCount = 0. |
| **Concurrent delete:** Two admins delete same custom role; one succeeds, one gets 404. | Second request: role already deleted → 404. Acceptable. |
| **Concurrent reassignment:** Admin A reassigns user from X to Y; Admin B deletes role X. | If B’s delete runs after A’s update, role X has 0 users and can be deleted. If B’s delete runs before A’s update, delete fails with 409. No inconsistency. |
| **Custom role name equals system role after casing change** | Prevent at create: case-insensitive match against Canonical. No “rename role” in scope; if added later, same check. |
| **User create with non-existent role** | Validate role exists; 400 with clear message (e.g. “Role does not exist.”). |
| **User update to empty role** | Reject: 400 “Role is required.” or “Role cannot be cleared.”. |
| **GET /roles returns legacy role (e.g. Kellner)** | After migration, Kellner is removed from seed and from DB for new installs. Existing DB: migration reassigns users and deletes Kellner. So GET /roles no longer returns it. If a legacy role still exists, it appears in list; UI can treat “not in canonical” as custom (editable/deletable if 0 users). |
| **Demo role in DB** | Migration: remove Demo from seed; data migration to unassign any user from Demo (e.g. set to Cashier) and delete Demo role. PaymentService: use IsDemo for restrictions; optionally keep Role == "Demo" check during transition. |
| **Permission list for system role in UI** | Read-only display; no save. Backend already rejects PUT permissions for system roles. |

---

## Implementation checklist (no code in this doc)

- [ ] **Backend:** Require role on user create; validate role exists. Return 400 if missing or invalid.
- [ ] **Backend:** On user update, reject empty role; validate new role exists when role is being changed.
- [ ] **Backend:** CreateRole: reject if name is in Roles.Canonical (case-insensitive); 400 ROLE_NAME_RESERVED.
- [ ] **Backend:** Optional: add userCount to 409 body for DELETE role when ROLE_HAS_ASSIGNED_USERS.
- [ ] **Backend:** Optional: GET /roles filter to assignable roles only (canonical + custom); or document that UI should filter.
- [ ] **Frontend:** Role dropdown: only assignable roles; role required in user form; validation.
- [ ] **Frontend:** When userCount > 0 on selected custom role, block delete in UI and show reassignment message (and optionally link to user list).
- [ ] **Frontend:** System role: keep delete and permission edit disabled; show “System” badge/label.
- [ ] **Docs/tests:** Document R1–R7 and add/update tests for role delete (system, with users, zero users), user create/update (role required, invalid role), CreateRole (reserved name).
