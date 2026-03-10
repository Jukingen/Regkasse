# Role Management UI – Architecture Note

**Updated:** 2025-03-10 (aligned with backend role-governance and simplified POS role model)

## Data flow

- **Source:** `GET /api/UserManagement/roles/with-permissions` returns `RoleWithPermissionsDto[]` with `roleName`, `permissions`, `isSystemRole`, `userCount`, `canDelete`, `canEditPermissions`.
- **Hook:** `useRolesWithPermissions()` loads roles and exposes them to the users page; Role Management Drawer receives `roles` and `catalog` as props.
- **Permissions:** Create/delete/edit role visibility is driven by `canCreateRole`, `canDeleteRole`, `canEditRolePermissions` (from `useAuth` + role helpers). Actual delete/save is disabled when the **selected role** is not deletable or not editable (`selectedRoleCanDelete`, `canEditRole`).

## UI behavior

- **Sorting:** System roles first (alphabetical), then custom roles (alphabetical). Implemented in `sortedRoles` in `RoleManagementDrawer`.
- **Badges:** Each role in the list shows a `Tag`: "System" (blue) or "Benutzerdefiniert" (default). User count is shown below the role name.
- **Display names:** Known system roles (e.g. SuperAdmin, Cashier) are shown with German labels (e.g. "Super-Administrator", "Kassierer") via `usersCopy.roleDisplayName(roleName)`. Unknown/custom roles show the raw `roleName`.
- **Delete button:** Enabled only when the current user has `canDeleteRole` and the selected role has `selectedRoleCanDelete` (backend `canDelete` or derived: custom role with zero users). Tooltip explains why delete is disabled: system role vs. "reassign users first".
- **Helper text:** When a **system role** is selected, an `Alert` states that system roles are protected and cannot be deleted (`systemRoleProtectedNoDelete`). When a **custom role** with `userCount > 0` is selected, an `Alert` explains that the role cannot be deleted until users are reassigned (`roleDeleteBlockedReassignFirst`).
- **Save / checkboxes:** Save and permission checkboxes are disabled when the selected role is not editable (`!canEditRole`), i.e. system roles. Preset selector is only shown when the selected role is editable.

## Files

| File | Purpose |
|------|--------|
| `components/RoleManagementDrawer.tsx` | Drawer UI: role list (sorted, badges, display names), permissions panel, delete/save actions, helper alert. |
| `constants/copy.ts` | German strings: `systemRoleProtectedNoDelete`, `roleDeleteBlockedReassignFirst`, `badgeSystemRole`, `badgeCustomRole`, `roleDisplayName`, `ROLE_DISPLAY_NAMES`. |
| `api/roleManagementApi.ts` | `RoleWithPermissionsDto` includes optional `canDelete`, `canEditPermissions`. |

## UI behavior changes (governance alignment)

- **System vs custom:** Tag "System" (blue) / "Benutzerdefiniert" and Alert when system role selected.
- **Delete disabled:** When `canDelete` is false (system role or role with users), delete button is disabled; tooltip shows reason.
- **Helper text:** System role → "Systemrollen sind geschützt und können nicht gelöscht werden."; custom with users → "Diese Rolle kann nicht gelöscht werden, weil noch Benutzer zugewiesen sind. Weisen Sie diese Benutzer zuerst einer anderen Rolle zu."
- **Sorting:** System roles first, then custom; alphabetical within each group (`localeCompare(..., 'de')`).
- **User count:** Shown under each role name via `usersCopy.userCount(r.userCount)`.

## Risks

- **Stale metadata:** If backend adds a role or changes userCount after the drawer opened, the list does not refresh until roles are refetched (parent responsibility).
- **Fallback:** When backend omits `canDelete`/`canEditPermissions`, UI derives from `isSystemRole` and `userCount` so behavior stays correct.

## Out of scope

- Users table and user create/edit flows are unchanged.
- No archive/deactivate role in UI; backend supports only hard delete when `userCount === 0`.
