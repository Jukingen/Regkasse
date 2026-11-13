# Admin User-Role Assignment – Verification Report

**Date:** 2025-03-15  
**Branch:** checked-in code (current state)  
**Scope:** frontend-admin only; no POS; no regulation-critical areas.

---

## 1. Conclusion

**No “assigned-only render” bug exists in this branch.** The only user-role assignment UI is already catalog-driven. Options always come from the full role catalog; the selected user’s role is used only for the checked/selected value. No secondary or alternate role-assignment path was found that renders from assigned data only.

**Likely causes if the bug is still seen in production:** older deployed frontend, stale browser cache, or environment-specific bundle. No backend or API contract changes are required.

---

## 2. Files Inspected

| File                                                     | Purpose                                                                    | Verdict                                                                        |
| -------------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `src/app/(protected)/users/page.tsx`                     | Users list, edit/create state, roleOptions, useRoles, UserFormDrawer props | **OK** – roleOptions from `roles` only; no user in deps                        |
| `src/features/users/components/UserFormDrawer.tsx`       | Create/Edit user form; role field (Radio.Group)                            | **OK** – options = roleOptions (catalog); value = user.role                    |
| `src/features/users/components/UserDetailDrawer.tsx`     | View user; shows user.role in a Tag                                        | **OK** – read-only display, no role selection                                  |
| `src/features/users/components/RoleManagementDrawer.tsx` | Manage role permissions (which permissions a role has)                     | **N/A** – not user-role assignment; no “selected user”; lists all system roles |
| `src/features/users/hooks/useRoles.ts`                   | Load role catalog (GET /api/UserManagement/roles)                          | **OK** – returns full list                                                     |
| `src/features/users/hooks/useRolesWithPermissions.ts`    | Used by RoleManagementDrawer only                                          | **N/A** – for role↔permissions, not user↔roles                                 |
| `src/features/users/api/usersGateway.ts`                 | getRoles, getUserById, query keys                                          | **OK** – catalog and user detail separate                                      |
| `src/features/users/utils/roleAssignmentMerge.ts`        | Helpers for assigned vs catalog semantics                                  | **OK** – used in tests/docs only                                               |
| `src/shared/auth/AdminOnlyGate.tsx`                      | Uses user?.role for redirect                                               | **OK** – no role selection UI                                                  |
| `src/app/(protected)/layout.tsx`                         | Menu visibility by role                                                    | **OK** – no role assignment UI                                                 |

Searched for: any modal/drawer/table that (a) receives a selected user and (b) builds role options from that user or from “assigned” list. **None found.** Only one user-role assignment flow: Edit User → UserFormDrawer → role field.

---

## 3. Data Flow Verified

### Role catalog (visible list)

- **Source:** `useRoles({ enabled: policy.canView || !!editUserId })` → `getRoles()` → `GET /api/UserManagement/roles`.
- **Derivation:** `roleOptions = roles?.map(r => ({ value: r, label: r })) ?? ROLE_OPTIONS`. Dependency: `[roles]` only. **No `user` or `editUserFull` in this useMemo.**
- **Usage:** Passed to UserFormDrawer as `roleOptions`; used for table role filter `options={roleOptions}`. Same catalog everywhere.

### Selected user / checked state

- **Source:** `getUserById(editUserId)` → `GET /api/UserManagement/{id}` → `UserInfo.role` (single string).
- **Usage:** In UserFormDrawer, `userToFormValues(user).role` sets form field `role`. Radio.Group value = that form value. Only the one assigned role is checked.

### User switch / state reset

- **Form key:** `key={editUserId ?? 'edit'}` on UserFormDrawer; form key inside is `edit-${user.id}`. When `editUserId` or `user.id` changes, the form remounts → no stale selection.
- **Sync effect:** When `user` changes (edit mode), effect runs and `setFieldsValue(userToFormValues(user))` so the form reflects the new user.

### Save and rehydration

- **Mutation:** `updateMutation` calls `gatewayUpdateUser(id, data)` → `PUT /api/UserManagement/{id}`.
- **onSuccess:** `invalidateQueries(getUserByIdQueryKey(id))`, `invalidateQueries(listQueryKey)`, `setEditUserId(null)`. Next time the same user is opened, user detail is refetched.

### Loading / empty

- **UserFormDrawer:** When `rolesLoading && roleOptions.length === 0`, shows “Rollen werden geladen…” instead of rendering an option list. So the UI never shows a list built only from the assigned role.

---

## 4. What Could Have Been Wrong (Not Present)

- **Building options from user.role or user.roles:** Not done. `roleOptions` is never derived from `user` or `editUserFull`.
- **Rendering from assignedRoles only:** No such variable. The only “assigned” is the single form value `role` from `user.role`.
- **State leak on user switch:** Form is keyed by user id; sync effect updates from new user. No shared “draft roles” that would carry over.
- **Skipping full catalog while roles unresolved:** When catalog is loading and empty, the drawer shows loading text, not a single-option list.

---

## 5. Changes Made (Minimal Hardening)

| File                                 | Change                                                                                                                                                            |
| ------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/(protected)/users/page.tsx` | One comment added above `roleOptions` useMemo: _“Role list for form + filter: always from full catalog (useRoles). Never from selected user or assigned subset.”_ |

No logic change. Comment makes the invariant explicit for future edits.

---

## 6. API Contract

- **No backend or contract changes.** Still: `GET /api/UserManagement/roles`, `GET /api/UserManagement/{id}`, `PUT /api/UserManagement/{id}` with single `role` on request/response.

---

## 7. Manual Verification Steps

If the bug is still reported after deployment:

1. **Hard refresh / clear cache:** DevTools → Application → Clear site data, or open in incognito. Reload and test again.
2. **Confirm deployed bundle:** Ensure the deployed frontend includes the current users page and UserFormDrawer (Radio.Group + roleOptions from parent, loading state when catalog empty).
3. **Catalog in edit:** Users → Edit a user → Role field shows all roles from API (or ROLE_OPTIONS fallback) as radio buttons; only that user’s role is selected.
4. **User switch:** With edit drawer open, click Edit on another user → Form updates; only the new user’s role is selected.
5. **Save and re-open:** Change role, Save, open same user again → Role shown is the one just saved (from backend).

---

## 8. Regression Test

- **`src/features/users/utils/__tests__/roleAssignmentMerge.test.ts`** already has:
  - “full catalog length is independent of assigned (display list from catalog, not assigned)”,
  - “user switch: assigned comes from new user only (no stale state)”.
- So the semantics “visible list = catalog, checked = assigned” are covered by tests.
