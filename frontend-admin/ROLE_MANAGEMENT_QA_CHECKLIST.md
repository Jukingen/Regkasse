# Role Management – Manual QA Checklist

**Test coverage (automated):** Backend: 13 RoleManagementTests (system role delete 400, assigned users 409, SuperAdmin-only 403, invalid key 400, role not found 404, empty permissions 200, catalog). Frontend: useRolesWithPermissions, usePermissionsCatalog, roleManagementApi (update/delete/save fail), rolePresets (preset apply, dirty, manual override), validateCatalogAlignment, roleManagementDrawer.logic (delete next selection, dirty-state, system role). Users page test mock updated for new gateway exports; one existing test (reset password flow) may still fail in CI—see edge cases.

## Backend (API)

- [ ] **GET /api/UserManagement/roles/permissions-catalog** – returns 200, array of `{ key, group, resource, action, description? }`; requires UserView.
- [ ] **GET /api/UserManagement/roles/with-permissions** – returns 200, array of roles with `roleName`, `permissions`, `isSystemRole`, `userCount`; requires UserView.
- [ ] **PUT .../roles/{roleName}/permissions** – as **non-SuperAdmin** (e.g. Manager): 403. As **SuperAdmin** with custom role: 200; SuperAdmin role matrix-only: 400; with invalid key: 400; role not found: 404. Empty `permissions` array: 200.
- [ ] **DELETE .../roles/{roleName}** – as **non-SuperAdmin**: 403. As **SuperAdmin** with custom role, no users: 200; with system role: 400; role has users: 409; role not found: 404.

## Frontend – Users Page

- [ ] **Rollen verwalten** button visible only for SuperAdmin (or user with role management capability).
- [ ] Click **Rollen verwalten** opens drawer; title "Rollen verwalten" and description about menu visibility.

## Frontend – Role Management Drawer

### Initial render & states

- [ ] **Loading**: drawer shows spinner while roles or catalog are loading.
- [ ] **Error**: error alert with "Erneut versuchen" when roles or catalog fail.
- [ ] **Empty roles**: left panel shows empty state; right panel shows "Rolle auswählen".
- [ ] **With data**: left panel list sorted alphabetically; first role selected by default; right panel shows grouped permissions.

### Permissions

- [ ] **Custom role**: checkboxes enabled; preset dropdown visible; Save and Delete enabled (Delete only if no users).
- [ ] **System role**: checkboxes disabled; preset dropdown hidden or disabled; Delete button disabled with tooltip "Systemrollen können nicht gelöscht werden."
- [ ] **Preset apply**: choose a preset (e.g. "Rapor Görüntüleme") → draft permissions replace with preset (only keys in catalog); Save becomes enabled if different from saved.
- [ ] **Manual override**: after preset, toggle a checkbox → draft updates; dirty remains until Save.

### Save & Delete

- [ ] **Save**: with dirty and custom role, click Save → success toast; list refetches; draft no longer dirty.
- [ ] **Save fail**: simulate 400/404 → error toast.
- [ ] **Delete**: custom role, 0 users → confirm → success toast; next role in list selected; list refetches.
- [ ] **Delete fail (users assigned)**: custom role with userCount > 0 → warning "Rolle kann nicht gelöscht werden: mindestens ein Benutzer ist zugewiesen."
- [ ] **Delete fail (API 409)**: toast with error message.

### Dirty-state confirm

- [ ] **Close with dirty**: click drawer close (X) with unsaved changes → confirm "Ungespeicherte Änderungen verwerfen?"; Verwerfen closes, Dranbleiben keeps open.
- [ ] **Switch role with dirty**: select another role with unsaved changes → same confirm; Verwerfen switches and loads new role draft.

### Delete next selection

- [ ] After successful delete, the selected role is the first remaining in the list (alphabetically). If no roles left, selection is empty.

## Quick smoke

1. Log in as SuperAdmin.
2. Open Users → Rollen verwalten.
3. Select a custom role (or create one), apply preset "Kasa Operasyon", click Save.
4. Select same role again, delete (if 0 users), confirm → role removed, another role selected.
5. Close drawer with dirty changes → confirm appears.
