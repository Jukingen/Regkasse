# Admin User-Role Assignment – Minimal Fix Summary

## Changed files

| File | Change type |
|------|-------------|
| `src/features/users/components/UserFormDrawer.tsx` | Role UI: catalog-driven Radio.Group; rolesLoading state; fullRoleCatalog/assignedRoleIds comment |
| `src/app/(protected)/users/page.tsx` | useRoles enabled when edit open; invalidate user-detail on save; filter uses catalog; pass rolesLoading |
| `src/features/users/constants/copy.ts` | Added `rolesLoading` string |
| `src/features/users/utils/roleAssignmentMerge.ts` | **New.** Helpers: `getAssignedRoleIdsFromUser`, `isRoleChecked` (single-role model) |
| `src/features/users/utils/__tests__/roleAssignmentMerge.test.ts` | **New.** Unit tests: catalog vs assigned merge, user switch no stale state |
| `src/app/(protected)/users/__tests__/page.test.tsx` | useRoles mock `isLoading: false`; mockGetUserById in edit test; invalidation-after-save test |

---

## Exact logic changes

### UserFormDrawer.tsx
- **Role field source:** Options are no longer a single Select; they are the **full catalog** `roleOptions` (from parent). Assigned state is used **only** for the checked value: form field `role` is set from `user.role` in edit mode via `userToFormValues(user)` and the sync effect.
- **Role UI:** Replaced `<Select options={roleOptions} />` with:
  - When `rolesLoading && roleOptions.length === 0`: show `usersCopy.rolesLoading` text (no subset list).
  - Otherwise: `<Radio.Group options={...} />` where options = `roleOptions.map(opt => ({ value: opt.value, label: usersCopy.roleDisplayName(opt.value) }))`. Form value `role` controls which radio is checked.
- **Reset on user change:** Form key remains `edit-${user.id}` (from parent `key={editUserId ?? 'edit'}`). When the selected user changes, the key changes → form remounts → no stale checked state.
- **New prop:** `rolesLoading?: boolean` so the drawer can show loading when the catalog is not yet available.

### page.tsx
- **useRoles:** `enabled: policy.canView || !!editUserId` so the role catalog is fetched when the edit drawer is open (not only when the page has canView and was already loaded).
- **roleOptions:** Still `roles?.map(r => ({ value: r, label: r })) ?? ROLE_OPTIONS` — always full catalog (or fallback); never derived from the selected user.
- **updateMutation.onSuccess:** Now receives `(_data, { id })` and calls `queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(id) })` before `listQueryKey` invalidation and `setEditUserId(null)`. Ensures rehydration from backend when the same user is reopened.
- **Filter Select:** `options={ROLE_OPTIONS}` changed to `options={roleOptions}` so the table role filter uses the same catalog (including custom roles from API).
- **Drawers:** Both create and edit `UserFormDrawer` receive `rolesLoading={rolesLoading}` (from `useRoles`’s `isLoading`).

### copy.ts
- **New key:** `rolesLoading: 'Rollen werden geladen…'` used when the role catalog is loading and the list is empty.

---

## API contract changes

- **None.** Backend and OpenAPI unchanged. Still:
  - `GET /api/UserManagement/roles` → list of role names (catalog).
  - `GET /api/UserManagement/{id}` → `UserInfo` with single `role`.
  - `PUT /api/UserManagement/{id}` → `UpdateUserRequest` with single `role`.

---

## Query invalidation / refetch

| When | What |
|------|------|
| **Edit drawer opens** | `useRoles` runs if `policy.canView || !!editUserId`; catalog loads so `roleOptions` is full. |
| **User detail (edit)** | Existing `useQuery(getUserByIdQueryKey(editUserId), getUserById)` when `!!editUserId`; form sync effect sets fields from `user` when `user` is set. |
| **After update success** | `queryClient.invalidateQueries({ queryKey: getUserByIdQueryKey(id) })` so next open of that user refetches; `queryClient.invalidateQueries({ queryKey: listQueryKey })` so list refetches; `setEditUserId(null)` closes drawer. |
| **User switch** | Parent passes new `editUserId` → new `user` (from refetched or cached query); form key `editUserId` changes → form remounts → values from new `user`, no stale role selection. |

No new queries or mutations; only invalidation and `enabled` logic changed.

---

## Manual test checklist

- [ ] **Catalog in edit:** Open Users → Edit a user. Role section shows **all** roles from the catalog (API or fallback) as radio buttons; **only** that user’s current role is selected.
- [ ] **Create flow:** Benutzer anlegen → Role section shows full catalog; choose one role and save. User is created with that role; single-role flow unchanged.
- [ ] **User switch:** With edit drawer open, click Edit on another user (different row). Drawer content updates; role selection shows the **new** user’s role only; no carry-over from the previous user.
- [ ] **Rehydrate after save:** Edit a user, change role, Save. Re-open the same user (or same row). Role shown is the updated one from the backend.
- [ ] **Loading state:** If roles load slowly, with edit open the role field shows “Rollen werden geladen…” until options are available; then full list appears.
- [ ] **Table filter:** Role filter dropdown shows the same catalog as the form (including custom roles when returned by API). Filtering by role still works.
- [ ] **No regression:** Create user (with role), deactivate, reactivate, reset password, view detail — all unchanged. No changes to POS or regulation-critical areas.

---

## Test plan (added)

### Unit tests
- **`roleAssignmentMerge.test.ts`:** `getAssignedRoleIdsFromUser` returns `[]` for null/empty, `[user.role]` for single role; `isRoleChecked` true only when role in assigned list; catalog length independent of assigned; user switch yields new user’s assigned only (no leak).
- **page.test.tsx:** useRoles mock includes `isLoading: false`; edit test sets `mockGetUserById.mockResolvedValue(sampleUser)` so query has data; new test “invalidates user detail query on update success so UI rehydrates from backend” spies `queryClient.invalidateQueries` and asserts it was called with `getUserByIdQueryKey(id)`.

### Manual / integration
- Kullanıcı seç → tüm roller katalogdan görünür, sadece atanmış checked.
- Başka kullanıcıya geç → önceki checked state taşınmaz.
- Rol değiştir, Save → success → tekrar açınca backend’den güncel rol.
- Panel kapat/aç veya refresh → state backend ile tutarlı.
