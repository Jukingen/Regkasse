# Role-Governance Backend Implementation Summary

**Date:** 2025-03-10  
**Scope:** Backend changes for revised role-governance model (system roles protected, role required for users, clear delete/409 with userCount).

---

## Changed Files

| File | Changes |
|------|---------|
| `backend/Services/IRoleManagementService.cs` | Added `CanDelete`, `CanEditPermissions` to `RoleWithPermissionsDto`. |
| `backend/Services/RoleManagementService.cs` | Populate `CanDelete` (= !isSystemRole && userCount == 0), `CanEditPermissions` (= !isSystemRole) in `GetRolesWithPermissionsAsync`. |
| `backend/Controllers/UserManagementController.cs` | CreateUser: require non-empty role, validate role exists (400 ROLE_REQUIRED / ROLE_NOT_FOUND); fail role assignment with 400 ROLE_ASSIGN_FAILED. UpdateUser: require non-empty role, validate role exists before loading user; reject empty/invalid role. CreateRole: reject reserved (system) name with 400 ROLE_NAME_RESERVED. DeleteRole: 409 body includes `userCount` and message "Reassign them to another role before deleting." CreateUserRequest/UpdateUserRequest: `Role` required with `AllowEmptyStrings = false`. |
| `backend/KasseAPI_Final.Tests/RoleManagementTests.cs` | DeleteRole 409 test asserts `code`, `message`, `userCount`. New test: CreateRole_WhenNameIsSystemRole_Returns400WithROLE_NAME_RESERVED. |
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | CreateInMemoryUserManagerWithUsersAsync: use real RoleStore, seed roles via RoleManager.CreateAsync (Admin, Cashier, Manager, SuperAdmin, Waiter). CreateMockUserAndRoleManagers: setup roleStore.FindByNameAsync to return IdentityRole(name) so controller role validation passes. New tests: CreateUser_WhenRoleEmpty_Returns400, CreateUser_WhenRoleNotFound_Returns400, UpdateUser_WhenRoleEmpty_Returns400, UpdateUser_WhenRoleNotFound_Returns400. |

---

## API Examples

### GET /api/UserManagement/roles/with-permissions

Response items now include `canDelete` and `canEditPermissions`:

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

### DELETE /api/UserManagement/roles/{roleName}

- **System role:** `400`  
  `{ "message": "System roles cannot be deleted.", "code": "SYSTEM_ROLE_NOT_DELETABLE" }`

- **Custom role with users:** `409`  
  `{ "message": "Cannot delete role: one or more users are assigned to this role. Reassign them to another role before deleting.", "code": "ROLE_HAS_ASSIGNED_USERS", "userCount": 3 }`

- **Custom role with zero users:** `200`  
  `{ "message": "Role deleted successfully" }`

### POST /api/UserManagement/roles

- **Reserved name (e.g. Admin):** `400`  
  `{ "message": "Role name is reserved for system roles. Choose a different name for a custom role.", "code": "ROLE_NAME_RESERVED", "errors": { "Name": ["This role name is reserved."] } }`

### POST /api/UserManagement (create user)

- **Missing/empty role:** `400`  
  `{ "message": "Role is required. Users must have a valid role.", "code": "ROLE_REQUIRED", "errors": { "Role": ["Role is required."] } }`

- **Non-existent role:** `400`  
  `{ "message": "The specified role does not exist.", "code": "ROLE_NOT_FOUND", "errors": { "Role": ["Role does not exist."] } }`

### PUT /api/UserManagement/{id} (update user)

- **Empty role:** `400`  
  `{ "message": "Role is required. Users must have a valid role.", "code": "ROLE_REQUIRED", ... }`

- **Non-existent role:** `400`  
  `{ "message": "The specified role does not exist.", "code": "ROLE_NOT_FOUND", ... }`

---

## Test Results

- **RoleManagementTests:** All tests pass (including DeleteRole 409 with userCount/code/message, CreateRole reserved name 400).
- **UserManagementControllerUserLifecycleTests:** All tests pass (including new role-required and role-not-found cases; existing tests still pass with mock role store and in-memory role seed).

**Total:** 52 tests passed (filter: RoleManagementTests | UserManagementControllerUserLifecycleTests).

---

## Known Limitations

1. **Archive / deactivate role:** Not implemented. Only hard delete for custom roles with zero users. Soft-delete or “archive” would require schema/behavior change and is out of scope.
2. **Bulk reassign:** No endpoint to “reassign all users from role A to role B”. Reassignment is done by updating each user (PUT user with new role), then deleting the role.
3. **GET /roles:** Still returns all AspNetRoles. Filtering to “assignable only” (e.g. canonical + custom) is not applied; frontend can use `roles/with-permissions` and derive assignable list from that.
4. **Permission-first:** Unchanged; menu and endpoint authorization remain permission-based; role is the source of permissions only.

---

## Acceptance Criteria (Met)

- Assigned-user role deletion is rejected (409, clear message, userCount).
- System-role deletion is rejected (400 SYSTEM_ROLE_NOT_DELETABLE).
- Custom role deletion works only when userCount = 0.
- Returned errors are understandable for the UI (code + message; 409 includes userCount).
- Existing auth behavior is not broken (permission checks and token flow unchanged).
