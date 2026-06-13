# Role Management – Manual QA Checklist

**Related:** Truth-critical RKSV/invoice/reconciliation QA — `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` (same operational style: loading/error/empty, mutations, invalidation, negative paths).

**Hub doc:** [`docs/ACCESS_AND_ROLES_HUB.md`](docs/ACCESS_AND_ROLES_HUB.md)

**Test coverage (automated):** Backend: `RoleManagementTests`, `AdminAppPermissionProfileTests`, `RoleAdminMenuContractTests`. Frontend: `test:contract` (menu visibility + sidebar navigation), `users/page.test.tsx` (SuperAdmin unified wiring + Manager tenant list), role preset/drawer unit tests.

## Backend (API)

- [ ] **GET /api/UserManagement/roles/permissions-catalog** – returns 200, array of `{ key, group, resource, action, description? }`; requires UserView.
- [ ] **GET /api/UserManagement/roles/with-permissions** – returns 200, array of roles with `roleName`, `permissions`, `isSystemRole`, `userCount`; requires UserView.
- [ ] **PUT .../roles/{roleName}/permissions** – as **non-SuperAdmin** (e.g. Manager): 403. As **SuperAdmin** with custom role: 200; SuperAdmin role matrix-only: 400; with invalid key: 400; role not found: 404. Empty `permissions` array: 200.
- [ ] **DELETE .../roles/{roleName}** – as **non-SuperAdmin**: 403. As **SuperAdmin** with custom role, no users: 200; with system role: 400; role has users: 409; role not found: 404.
- [ ] **POST /api/Auth/login** with `app_context=admin` – Cashier receives admin-whitelisted permissions only; Manager omits POS-terminal keys in JWT/`/me`.

## Frontend – Access hub navigation

- [ ] **Manager:** Verwaltung → Zugriff & Rollen shows overview, Benutzer, Rollen, Matrix per `ROLE_VIEW` / `USER_VIEW`.
- [ ] **Cashier (admin login):** No users/roles hub; limited catalog/pricing/payments/report menus only.
- [ ] Secondary nav tabs on `/admin/access/*` and `/admin/users` (tenant) stay in sync.

## Frontend – Roles page (`/admin/access/roles`)

- [ ] Page loads full role editor (not drawer-only); title and grouped permissions visible.
- [ ] **Loading / error / empty** states same as former drawer behavior.

### Permissions

- [ ] **Custom role**: checkboxes enabled; preset dropdown visible; Save and Delete enabled (Delete only if no users).
- [ ] **System role**: checkboxes disabled where API forbids edit; Delete disabled with tooltip.
- [ ] **Preset apply**: choose a preset → draft permissions replace with preset (catalog keys only); Save enabled when dirty.
- [ ] **Manual override**: after preset, toggle a checkbox → draft updates.

### Save & Delete

- [ ] **Save**: success toast; list refetches; draft no longer dirty.
- [ ] **Delete**: custom role, 0 users → confirm → success; next role selected.
- [ ] **Delete blocked**: role with users → warning / 409 toast.

### Dirty-state confirm

- [ ] Navigate away or switch role with unsaved changes → discard confirm.

## Frontend – Matrix page (`/admin/access/matrix`)

- [ ] Read-only table lists roles with permission counts and POS/admin split columns.
- [ ] Link or hint to `/admin/access/roles` for edits (when permitted).

## Quick smoke

1. Log in as **Manager** on tenant host.
2. Verwaltung → Zugriff & Rollen → Rollen & Berechtigungen.
3. Select a custom role, apply preset, Save.
4. Open Berechtigungsübersicht; verify matrix matches saved role.
5. Benutzer tab → create/edit user still works; “Rollen verwalten” opens roles page.

6. Log in as **Cashier** (admin) – confirm hub hidden; POS-relevant menus only.
