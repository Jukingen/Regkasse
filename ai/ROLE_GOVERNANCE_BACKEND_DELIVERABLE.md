# Role Governance – Backend Implementation Deliverable

**Date:** 2025-03-10  
**Status:** Implemented; safety guards and role metadata are in place. No new code this pass—verification and summary.

---

## Before coding (summary)

- **Affected files:** See Section 1. Core: `Roles.cs`, `IRoleManagementService.cs`, `RoleManagementService.cs`, `UserManagementController.cs`, and role/user lifecycle tests.
- **Implementation plan:** (1) Service layer: reject delete for system role and for role with users; (2) Controller: map to 400/409 with `code` and `message`; (3) DTO: expose `roleName`, `isSystemRole`, `userCount`, `canDelete`, `canEditPermissions`; (4) Create/Update user: require role and validate role exists so users never become role-less.
- **Assumptions:** System roles = `Roles.Canonical` (8 names); permission-first auth unchanged; single role per user; hard delete only.

---

## 1) Affected files

| File | Purpose |
|------|--------|
| `backend/Authorization/Roles.cs` | Defines 8 system roles (Canonical). Source of truth for “system role”. |
| `backend/Services/IRoleManagementService.cs` | RoleWithPermissionsDto (roleName, isSystemRole, userCount, canDelete, canEditPermissions); DeleteRoleResult enum. |
| `backend/Services/RoleManagementService.cs` | GetRolesWithPermissionsAsync populates metadata; DeleteRoleAsync enforces system-role and assigned-user guards. |
| `backend/Controllers/UserManagementController.cs` | DeleteRole returns 400/409 with clear codes and message; CreateUser/UpdateUser require role and validate role exists. |
| `backend/KasseAPI_Final.Tests/RoleManagementTests.cs` | Tests for delete (system, has users, 409 body), CreateRole reserved name. |
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | Tests for role required and role not found on create/update; mock and in-memory role setup. |

---

## 2) Implementation plan (already applied)

1. **System roles not deletable:** RoleManagementService.DeleteRoleAsync checks `Roles.Canonical`; returns `DeleteRoleResult.SystemRoleNotDeletable`. Controller returns 400 with code `SYSTEM_ROLE_NOT_DELETABLE`.
2. **Role with users not deletable:** DeleteRoleAsync checks `GetUsersInRoleAsync`; returns `RoleHasAssignedUsers`. Controller returns 409 with `ROLE_HAS_ASSIGNED_USERS`, message, and `userCount`.
3. **Users never role-less:** CreateUser and UpdateUser require non-empty `request.Role` and validate role exists via `_roleManager.FindByNameAsync`; return 400 `ROLE_REQUIRED` or `ROLE_NOT_FOUND` otherwise.
4. **Clear domain errors:** All responses include `message` and `code`; 409 includes `userCount` for UI.
5. **Metadata for UI:** GetRolesWithPermissionsAsync returns DTOs with `roleName`, `isSystemRole`, `userCount`, `canDelete`, `canEditPermissions` (serialized camelCase by default).

---

## 3) Assumptions

- **System roles** are exactly the 8 in `Roles.Canonical` (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). No DB-driven list.
- **Permission-first** auth is unchanged: endpoints use `[HasPermission(...)]` or role-based policies; token and handler logic not modified.
- **Single role per user** (ApplicationUser.Role + one AspNetUserRoles entry). No multi-role reassignment in one request.
- **Delete** is hard delete only; no archive/soft-delete.

---

## 4) Endpoint examples

### GET /api/UserManagement/roles/with-permissions

**Response (200):** Array of objects:

```json
{
  "roleName": "Admin",
  "permissions": ["user.view", "user.manage", ...],
  "isSystemRole": true,
  "userCount": 2,
  "canDelete": false,
  "canEditPermissions": false
}
```

```json
{
  "roleName": "CustomRole",
  "permissions": ["sale.view"],
  "isSystemRole": false,
  "userCount": 0,
  "canDelete": true,
  "canEditPermissions": true
}
```

### DELETE /api/UserManagement/roles/Admin

**Response (400):** System role not deletable.

```json
{
  "message": "System roles cannot be deleted.",
  "code": "SYSTEM_ROLE_NOT_DELETABLE"
}
```

### DELETE /api/UserManagement/roles/MyCustomRole

**Response (409):** Role has assigned users.

```json
{
  "message": "Cannot delete role: one or more users are assigned to this role. Reassign them to another role before deleting.",
  "code": "ROLE_HAS_ASSIGNED_USERS",
  "userCount": 3
}
```

### DELETE /api/UserManagement/roles/EmptyCustomRole

**Response (200):** Success when custom role and zero users.

```json
{
  "message": "Role deleted successfully"
}
```

### POST /api/UserManagement (create user without role)

**Response (400):**

```json
{
  "message": "Role is required. Users must have a valid role.",
  "code": "ROLE_REQUIRED",
  "errors": { "Role": ["Role is required."] }
}
```

### POST /api/UserManagement (create user with non-existent role)

**Response (400):**

```json
{
  "message": "The specified role does not exist.",
  "code": "ROLE_NOT_FOUND",
  "errors": { "Role": ["Role does not exist."] }
}
```

---

## 5) Test results

**Filter:** `RoleManagementTests` + `UserManagementControllerUserLifecycleTests`.

**Result:** Passed: 52, Failed: 0.

**Coverage includes:**
- Delete: system role → 400; role with users → 409 (body has code, message, userCount); role not found → 404; success for custom zero-user.
- SetRolePermissions: system role → 400; not SuperAdmin → 403.
- CreateRole: reserved name (e.g. Admin) → 400 ROLE_NAME_RESERVED.
- CreateUser: empty role → 400 ROLE_REQUIRED; non-existent role → 400 ROLE_NOT_FOUND.
- UpdateUser: empty role → 400 ROLE_REQUIRED; non-existent role → 400 ROLE_NOT_FOUND.

---

## 6) Known limitations

- **Bulk reassign:** No endpoint to “reassign all users from role A to role B”. Admins must change each user’s role, then delete the role.
- **Unknown/typo roles:** Only canonical names are protected. Any other role in AspNetRoles is treated as custom (deletable when userCount = 0). No automatic migration of unknown role names.
- **Seed:** RoleSeedData seeds only the 8 canonical roles; legacy role names are no longer created by seed (see ai/ROLE_SEED_AND_LEGACY_MIGRATION_NOTES.md).
- **Permission-first:** Unchanged; no new permission constants or handler changes.
