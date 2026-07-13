# Access & roles hub (Zugriff & Rollen)

**Audience:** Frontend-admin maintainers, QA, Super Admin / Mandanten-Admin operators.  
**UI language:** German (de-AT). **Technical docs:** English.

Operational RBAC surfaces for tenant admins are grouped under **Verwaltung → Zugriff & Rollen** instead of scattering user and role tools across unrelated menu leaves.

---

## Routes

| Route | Purpose | Route guard (`routePermissions.ts`) |
|-------|---------|-------------------------------------|
| `/admin/access` | Hub landing (overview cards) | `USER_VIEW` |
| `/admin/users` | Tenant user lifecycle (list, create, deactivate, …) | `USER_VIEW` |
| `/admin/access/roles` | Role CRUD + permission editor (full page) | `ROLE_MANAGE` |
| `/admin/access/matrix` | Read-only role ↔ permission matrix summary | `ROLE_VIEW` |

Super Admin platform mode uses **`/users`** (redirects to `/admin/users` when needed) with `UnifiedAdminUsersView`; the access hub secondary nav is hidden for Super Admin layout.

Canonical path constants: `src/shared/accessAreaRoutes.ts`.

---

## Navigation IA

Sidebar registry (`src/shared/adminSidebarRegistry.ts`):

- **Verwaltung** group contains nested **`grp-access`** (Zugriff & Rollen):
  - Overview → `/admin/access`
  - Benutzer → `/admin/users`
  - Rollen & Berechtigungen → `/admin/access/roles`
  - Berechtigungsübersicht → `/admin/access/matrix`

Legacy top-level **Users** leaf under Verwaltung was removed; users live under the access group.

Secondary tabs on hub pages: `AccessSecondaryNav` (Settings-style horizontal nav), rendered in `admin/access/layout.tsx` and on `/admin/users` for tenant managers.

---

## Permission sources (single runtime truth)

Do **not** duplicate RBAC logic in the hub UI.

| Layer | Responsibility |
|-------|----------------|
| Backend | `RolePermissionMatrix.cs` — canonical role → permission keys |
| Admin login filter | `AdminAppPermissionProfile.cs` — JWT + `/me` permissions when `app_context=admin` (strips POS-only keys; Cashier admin whitelist) |
| Frontend `/me` | Same filtered permission list as JWT |
| Menus | `adminSidebarRegistry.ts` + `usePermissions()` |
| Routes | `routePermissions.ts` |
| Page actions | `usersPolicy`, `usePermissions()`, feature-level guards |

Contract tests:

- Backend: `RoleAdminMenuContractTests`, `AdminAppPermissionProfileTests`
- Frontend: `adminRoleMenuVisibility.test.ts`, `adminSidebarNavigation.test.ts` (in `npm run test:contract`)

---

## Admin vs POS permission split

Cashier (and other POS-primary roles) may sign in to FA in controlled cases. After login:

- Backend filters claims to **`AdminAppPermissionProfile`** (catalog subset for admin UI).
- Mandanten-Admin (`Manager`): admin permissions minus explicit POS-terminal keys.
- Cashier: small whitelist (`product.view`, `category.view`, `modifier.view`, `payment.view`, `report.view`).

POS continues to use full matrix permissions from login without admin filter.

Key files: `backend/Authorization/AdminAppPermissionProfile.cs`, `TokenClaimsService.cs`, `AuthController.cs` (`/api/Auth/login`, `/api/Auth/me`).

**Backup (Mandanten-Admin):** default role includes `backup.manage` (trigger + schedule) and `settings.view` (read routes). Platform backup surfaces (execution mode, artifact download) remain `settings.manage`. Details: [`docs/BACKUP_PERMISSIONS.md`](../../docs/BACKUP_PERMISSIONS.md).

---

## Role management UI

- **Full page:** `/admin/access/roles` — `RoleManagementDrawer` with `presentation="page"`.
- **Matrix:** `/admin/access/matrix` — `RoleMatrixOverview` (read-only; links to roles page for edits).
- **Users page:** “Rollen verwalten” / manage roles → link to `/admin/access/roles` (drawer no longer primary entry).

See also: `ROLE_MANAGEMENT_UI.md`, `src/features/users/README_ROLE_UI.md`.

---

## i18n

Namespace: **`access`** (`src/i18n/locales/{de,en,tr}/access.json`), registered in `localization/namespace-manifest.json` and `src/i18n/config.ts`.

Nav labels: `nav.accessHub`, `nav.accessOverview`, `nav.accessRoles`, `nav.accessMatrix`.

---

## Related tests

```bash
cd frontend-admin
npm run test:contract
npx vitest run "src/app/(protected)/users/__tests__/page.test.tsx"
npm run i18n:ci
```
