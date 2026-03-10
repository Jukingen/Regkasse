# Role Management – Known Edge Cases & Limitations

## Backend

- **System role permissions**: Defined in code (RolePermissionMatrix); PUT permissions for system role returns 400. UI should not allow editing.
- **Empty permission set**: Allowed; custom role can have zero permissions (PUT with `[]` returns 200).
- **Role name encoding**: Role names with spaces or special characters are URL-encoded (e.g. `Custom Role` → `Custom%20Role`). Backend receives decoded name.
- **Concurrent delete**: If two SuperAdmins delete the same role, one may get 404 after the first delete. No optimistic locking.

## Frontend

- **Catalog / menu alignment**: Menu and route permission keys are validated against the catalog on drawer open; unknown keys log a console warning. If backend adds a new permission and FE menu uses it before catalog is regenerated, warning appears.
- **Preset keys not in catalog**: Preset apply uses only keys that exist in the current catalog. If backend catalog is a subset of preset keys, some preset permissions are silently dropped in the draft.
- **Dirty state and refetch**: After Save, React Query invalidates and refetches. Draft is re-synced from the new `selectedRole`; if refetch is slow, user might briefly see old draft.
- **Delete last custom role**: After deleting the last role in the list, `selectedRoleName` becomes `null`; right panel shows empty state. No automatic redirect or message beyond success toast.
- **Ant Design in tests**: Full drawer render in jsdom fails (matchMedia, List, etc.). Preset/dirty/delete-next-selection are covered by unit tests (rolePresets.test, roleManagementDrawer.logic.test); initial render, load/error/empty, and button states are covered by manual QA.
- **Users page test (reset password)**: One test in `page.test.tsx` asserts `mockResetPassword` to have been called after submitting the reset-password modal. This may fail intermittently or if the modal/button selection does not trigger the mutation (e.g. form validation or timing). Not introduced by role management changes; gateway mock now includes `getUserById`, `getUserByIdQueryKey`, `getRolesWithPermissions`, `getPermissionsCatalog`, `updateRolePermissions`, `deleteRole`, and policy fields `canDeleteRole`, `canEditRolePermissions`.

## Security / Policy

- **SuperAdmin-only**: Permission update and role delete are enforced on backend (403 for non-SuperAdmin). Frontend hides or disables actions based on `canEditRolePermissions` / `canDeleteRole` (SuperAdmin or ROLE_MANAGE).
- **System role in list**: Even if a role name matches a system role (e.g. "Admin"), backend treats it by identity from RoleManager; system roles are not deletable and not editable for permissions.
