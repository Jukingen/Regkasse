# Admin User-Role Assignment Bug – Implementation Path Analysis

**Date:** 2025-03-15  
**Scope:** frontend-admin (Next.js 14 + AntD + TanStack Query) + backend .NET API  
**Goal:** Identify exact code paths, root cause, and minimal fix for “UI renders only roles already assigned to the selected user” and related state issues.

---

## 1. Relevant Files

### Frontend-admin

| File                                                     | Responsibility                                                                                                                                 |
| -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/(protected)/users/page.tsx`                     | Users list, edit/create state, `editUserId`, `useRoles()`, `roleOptions`, passes data to drawers                                               |
| `src/features/users/components/UserFormDrawer.tsx`       | Create/Edit user form; **single role** `Select`; receives `roleOptions` and `user`                                                             |
| `src/features/users/components/UserDetailDrawer.tsx`     | View user + activity; displays `user.role` (read-only)                                                                                         |
| `src/features/users/components/RoleManagementDrawer.tsx` | **Role ↔ permissions** editor (left: role list, right: permission checklist). **No “selected user”** – manages roles, not user→role assignment |
| `src/features/users/api/usersGateway.ts`                 | `getRoles()`, `getUserById()`, `rolesQueryKey`, `getUserByIdQueryKey(id)`                                                                      |
| `src/features/users/api/roleManagementApi.ts`            | `getRolesWithPermissions()`, `getPermissionsCatalog()` – used by **Role Management** drawer only                                               |
| `src/features/users/hooks/useRoles.ts`                   | `useQuery(rolesQueryKey, getRoles)` – list of role **names** (string[])                                                                        |
| `src/features/users/hooks/useRolesWithPermissions.ts`    | Used by Role Management drawer (roles + permissions), not by user edit form                                                                    |
| `src/api/generated/model/userInfo.ts`                    | `UserInfo` with `role?: string \| null` (single role)                                                                                          |

### Backend

| File                                      | Responsibility                                                                                                                                       |
| ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Controllers/UserManagementController.cs` | `GET /api/UserManagement/roles` → all role names; `GET /api/UserManagement/{id}` → `UserInfo` (single `Role`); `PUT {id}` → `UpdateUserRequest.Role` |
| `Controllers/UserManagementController.cs` | `GET roles/with-permissions` → full role catalog with permissions (for Role Management drawer)                                                       |
| `Services/RoleManagementService.cs`       | `GetRolesWithPermissionsAsync()` → all roles from `_roleManager.Roles`                                                                               |

---

## 2. Current Data Flow

### User–role assignment (edit user)

1. **Open edit:** User clicks Edit → `setEditUserId(record.id)`.
2. **Load user:** `useQuery(getUserByIdQueryKey(editUserId), getUserById)` → `editUserFull` (includes `role`).
3. **Role catalog:** `useRoles({ enabled: policy.canView })` → `roles` (string[]) from `GET /api/UserManagement/roles`. Same query for whole page, not scoped to drawer.
4. **Options:** `roleOptions = roles?.map(r => ({ value: r, label: r })) ?? ROLE_OPTIONS` (static fallback when `roles` is undefined/empty).
5. **Form:** `UserFormDrawer` receives `user={editUserFull}`, `roleOptions={roleOptions}`. Form key: `edit-${user.id}` so form remounts when user changes. `userToFormValues(user)` sets `role: user.role`. Single `<Select options={roleOptions} />` for role.

### Role Management drawer (roles ↔ permissions)

- **Not** user-role assignment. It lists all roles (from `getRolesWithPermissions()`), user selects a **role** and edits its permissions. No “selected user” in this flow.

### Backend contracts

- **User:** Single role per user (`UserInfo.Role`, `UpdateUserRequest.Role`).
- **Catalog:** `GET /api/UserManagement/roles` returns all role names. `GET roles/with-permissions` returns full role list with permissions; used only by Role Management drawer.

---

## 3. Root Cause (Concrete)

- **“Renders only the roles already assigned to the selected user”** in this codebase can mean only the **Edit User** flow, where the role UI is a **single Select** whose options are `roleOptions`.
- So the bug is either:
  1. **Options are not the full catalog:** `roleOptions` is sometimes a list containing only the current user’s role (e.g. derived from `user` instead of always from catalog), or
  2. **Catalog is wrong:** `GET /api/UserManagement/roles` or the way it’s used returns/filters to one role (unlikely from backend code), or
  3. **Stale state when switching users:** Edit drawer keeps previous user’s role selected or previous options because of missing reset/invalidation when `editUserId` or `user` changes.

**In the current code:**

- `roleOptions` is **always** from `useRoles()` (full catalog) or `ROLE_OPTIONS` fallback; there is **no** code that sets `roleOptions` to `[user.role]`.
- Backend `GET /api/UserManagement/roles` returns all roles; `GetRolesWithPermissions` returns all roles. No filtering by current or selected user.
- So the most plausible causes are:
  - **Race/loading:** When opening edit, `roles` is still empty and we fall back to `ROLE_OPTIONS`. If the user’s role is not in `ROLE_OPTIONS`, the Select can look “single option” or show value not in options.
  - **Stale closure/cache:** `roleOptions` or form value from a previous user is reused (e.g. query not invalidated, or form not keyed/reset properly on user change).
  - **Ant Design Select:** If `value` (e.g. `user.role`) is not in `options` (e.g. custom role not in `ROLE_OPTIONS`), the Select can render in a way that looks like “only one option” or wrong option.

**Conclusion:** The bug is **frontend-only** in the sense that backend already exposes full role catalog and single `role` on user. The fix is to ensure: (1) role options are **always** from the full catalog (and drawer open when needed so catalog is loaded), (2) when switching users we **reset form/state** and rehydrate from server (key + invalidation already partially there), (3) handle loading/empty so we never show a dropdown driven only by the current user’s role.

---

## 4. Minimal Fix Path

### 4.1 Frontend (no backend contract change)

1. **Catalog as single source of truth**
   - Ensure the **edit** flow never builds role options from the selected user. Keep `roleOptions = roles?.map(...) ?? ROLE_OPTIONS`.
   - Optional: when opening edit drawer, ensure roles are loaded (e.g. `useRoles({ enabled: policy.canView || !!editUserId })` so opening edit doesn’t depend on a prior full page load).

2. **User switch: no stale state**
   - Keep `UserFormDrawer` keyed by user: `key={editUserId ?? 'edit'}` (or current `edit-${user.id}`) so the form remounts when switching users.
   - After successful save, invalidate `getUserByIdQueryKey(editUserId)` and list query so the next open gets fresh data.

3. **Rehydrate after save**
   - On successful `updateMutation`, already: `queryClient.invalidateQueries({ queryKey: listQueryKey })` and `setEditUserId(null)`. Add: `queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(editUserId) })` before closing so if the user reopens the same user, data is fresh.

4. **Loading/empty**
   - If `roles` is undefined/empty, keep using `ROLE_OPTIONS` but ensure it includes all system roles the backend can return (or show a short “Loading roles…” and disable role field until `roles` is loaded).

5. **Filter dropdown**
   - Table filter currently uses `ROLE_OPTIONS` (page.tsx ~499). For consistency, consider using the same `roleOptions` from API for the filter as well, so “full catalog” is consistent (optional, minimal change).

### 4.2 If the desired UI is “full catalog + checkboxes” (assigned = checked)

- Current domain is **single role per user**. To show “all roles, assigned checked”:
  - **Frontend:** One option is to keep a single role in the API but in the UI show a list of all roles (from catalog) with one selectable (radio/checkboxes) and send the selected one as `role` on save. That needs no backend change.
  - **Backend:** Only if product requires **multiple roles per user** would we need a new read model (e.g. `assignedRoleIds` or `roles: string[]`) and possibly new endpoint or extended `UserInfo`; not required for “full catalog + one selected”.

---

## 5. Backend Impact

- **No change required** for the minimal fix: `GET /api/UserManagement/roles` and `GET /api/UserManagement/{id}` already provide full catalog and user’s single role.
- If later we add a dedicated “assigned roles” read model (e.g. for multi-role), we could add something like `UserInfo.roleIds?: string[]` or keep `role` and derive list as `[user.role]` on the client.

---

## 6. Frontend Impact

| Area                 | Change                                                                                                                                                                                              |
| -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `users/page.tsx`     | Ensure `roleOptions` always from catalog; optional: enable `useRoles` when edit drawer open; invalidate `getUserByIdQueryKey` on successful update; optionally use `roleOptions` for filter Select. |
| `UserFormDrawer.tsx` | Keep key by user; ensure form reset when `user` or `open`/mode changes; no use of `user.role` to build options.                                                                                     |
| Hooks / API          | No change to `getRoles` or `getUserById`; optional refetch of roles when opening edit.                                                                                                              |

---

## 7. Endpoint / Permission Impact

- **No new endpoints.** Existing:
  - `GET /api/UserManagement/roles` – `HasPermission(UserView)` – used for role options.
  - `GET /api/UserManagement/{id}` – `HasPermission(UserView)` – used for edit form.
  - `PUT /api/UserManagement/{id}` – `HasPermission(UserManage)` – save user (including role).
- No permission change.

---

## 8. Risks

- **Low:** Fix is limited to ensuring catalog-driven options, correct invalidation, and form key/reset. No domain or API change.
- **Regression:** If we ever key or reset the form too aggressively, we might briefly clear user input; current key `edit-${user.id}` is appropriate.
- **Filter:** If we switch the table filter from `ROLE_OPTIONS` to `roleOptions`, filter options will match backend roles (including custom roles); if `roles` is slow to load, filter might be empty briefly.

---

## 9. Summary

- **Relevant UI:** User-role assignment = Edit User drawer in `users/page.tsx` + `UserFormDrawer.tsx` (single role Select). Role Management drawer is for role↔permissions, not user↔roles.
- **Root cause:** Frontend-only: options or state can be stale or incomplete (e.g. fallback list, or value not in options), or form state not fully reset when switching users.
- **Minimal fix:** Always use full catalog for `roleOptions`, key form by user and invalidate user-detail query on save, optional refetch of roles when opening edit and use same catalog for table filter.
- **Backend:** No contract or permission change needed for this minimal fix.

---

## 10. Implementation Summary (Done)

### Files changed

- **`frontend-admin/src/features/users/components/UserFormDrawer.tsx`**  
  Role selection is now catalog-driven: full `roleOptions` rendered as `Radio.Group`; only the user's role is checked (form value `role`). Added `rolesLoading` prop; when catalog is loading and options empty, show `usersCopy.rolesLoading` instead of a subset.
- **`frontend-admin/src/app/(protected)/users/page.tsx`**  
  `useRoles({ enabled: policy.canView || !!editUserId })` so roles load when edit drawer opens; `updateMutation.onSuccess` invalidates `getUserByIdQueryKey(id)` before closing; table filter Select uses `roleOptions` (catalog); both drawers receive `rolesLoading`.
- **`frontend-admin/src/features/users/constants/copy.ts`**  
  Added `rolesLoading: 'Rollen werden geladen…'`.

### Root cause

- Frontend rendered role options from the same source as the selected value; in edge cases (slow catalog, or dropdown showing only the selected value) it looked like "only assigned". Fix: always render from **full catalog** (roleOptions) and let **assigned state** only set the checked/selected value (Radio.Group value = `user.role`).
- Stale state on user switch: form was keyed by `user.id`; added invalidation of user-detail query on save so re-opening the same user gets fresh data.

### Endpoint contract

- No backend or API contract change. Still single `UserInfo.role`; GET `/api/UserManagement/roles` and GET `/{id}` unchanged.

### Queries/mutations/state

- **useRoles:** `enabled: policy.canView || !!editUserId` so catalog loads when edit drawer opens.
- **updateMutation.onSuccess:** `queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(id) })` plus existing list invalidation.
- **UserFormDrawer:** Key `edit-${user.id}` unchanged (resets form when switching users); role UI = Radio.Group with options from catalog, value from form.

### Manual verification

1. Open Users → Edit a user: all roles from catalog appear as radio list; only that user's role is selected.
2. Switch to another user (without closing drawer): form remounts (key change), new user's role is selected.
3. Change role, Save: list and cache update; open same user again, role is updated.
4. Create user: role list shows full catalog; select one and save.
5. Table filter: role dropdown shows same catalog (including custom roles when API returns them).
