# Role Management UI – Frontend Architecture Note

## Scope

Role and permission management for tenant admins lives in the **Access & roles hub** (`/admin/access/*`), not only on the users page.

- **Full page:** `/admin/access/roles` — list roles, view/edit permissions, create role, delete custom role.
- **Read-only matrix:** `/admin/access/matrix` — `RoleMatrixOverview` (links to roles page to edit).
- **Users page:** `/admin/users` — link **Rollen verwalten** → `/admin/access/roles` when `canCreateRole || canDeleteRole || canEditRolePermissions`.

Backend contract: GET permissions-catalog, GET with-permissions, PUT/DELETE role (unchanged).

Hub overview: [`docs/ACCESS_AND_ROLES_HUB.md`](docs/ACCESS_AND_ROLES_HUB.md).

## Role Labels (UI display vs backend)

Backend role names in database/API remain unchanged. Admin UI maps canonical roles to German display labels via `users.roles.displayNames` (`roleDisplayLabel.ts`):

| Backend (`Roles.cs`) | UI label (de) |
|----------------------|---------------|
| `SuperAdmin` | Super-Administrator |
| `Manager` | **Mandanten-Admin** |
| `Cashier` | Kassierer |
| `Waiter` | Kellner |
| `Kitchen` | Küche |
| `Accountant` | Buchhaltung |
| `ReportViewer` | Berichte (nur Lesen) |

Badge chips use `users.roles.badgeLabels` (shorter labels where applicable, e.g. SuperAdmin → Plattform-Admin).

## Presets

- **Data model:** `RolePreset { id, label, permissionKeys }` in `rolePresets.ts`. Preset apply replaces current draft with `getPresetKeysInCatalog(preset, catalogKeys)` (only keys present in catalog).
- **Semantics:** Apply updates only local draft state; no API call. User can then toggle checkboxes (manual override). Dirty = draft ≠ saved. Save persists; closing with dirty shows confirm.
- **Presets:** Kasa Operasyon, Muhasebe, Rapor Görüntüleme, Mağaza Yöneticisi.

## Affected Files

- `src/app/(protected)/admin/access/` — layout, hub landing, roles page, matrix page
- `src/features/access/` — `AccessSecondaryNav`, `RoleMatrixOverview`, `useRoleManagementWorkspace`
- `src/shared/accessAreaRoutes.ts` — canonical hub paths
- `src/shared/adminSidebarRegistry.ts` — `grp-access` nested group under Verwaltung
- `src/shared/auth/routePermissions.ts` — hub route guards
- `src/features/users/constants/rolePresets.ts` — Preset definitions and `getPresetKeysInCatalog`.
- `src/shared/auth/validateCatalogAlignment.ts` — Menu/route keys vs catalog; console warning when unknown.
- `src/features/users/api/roleManagementApi.ts` — DTOs and API calls for catalog, with-permissions, update, delete.
- `src/features/users/api/usersGateway.ts` — Query keys + re-exports for role management.
- `src/features/auth/constants/roles.ts` — `canDeleteRole`, `canEditRolePermissions` (SuperAdmin only).
- `src/shared/auth/usersPolicy.ts` — `canDeleteRole`, `canEditRolePermissions` in policy and actions.
- `src/features/users/hooks/useRolesWithPermissions.ts` — Hook.
- `src/features/users/hooks/usePermissionsCatalog.ts` — Hook.
- `src/features/users/components/RoleManagementDrawer.tsx` — Two-panel UI (`presentation: 'drawer' | 'page'`).
- `src/features/users/constants/copy.ts` — Role management copy (DE).
- `src/app/(protected)/users/page.tsx` — Manage-roles link; tenant list + Super Admin unified view.

## State

- **Server:** React Query for `rolesWithPermissions` and `permissionsCatalog`; enabled when roles workspace is open.
- **Local (editor):** `selectedRoleName`, `draftPermissions` (Set). Dirty = draft ≠ saved for selected role.
- **Mutations:** `updateRolePermissions`, `deleteRole`; on success invalidate `rolesWithPermissionsQueryKey` and `rolesQueryKey`.

## UX Rules

- Role list sorted alphabetically; first role selected on open.
- System roles: delete disabled + tooltip; SuperAdmin role permissions read-only (matrix-only).
- Dirty: confirm on close and on role switch; Save disabled when not dirty.
- Delete: confirm; after delete, select next role in list.
- "Neue Rolle" opens existing create-role modal (unchanged flow).

## Visibility

- Hub nav entries require `USER_VIEW` / `ROLE_VIEW` per route (`routePermissions.ts`).
- Role edit/delete/create actions: `canCreateRole || canDeleteRole || canEditRolePermissions` (typically SuperAdmin or `ROLE_MANAGE`).
- Admin session permissions come from filtered JWT (`AdminAppPermissionProfile` on backend).

## Lint

- `RoleManagementDrawer`: `useEffect` depends on `selectedRole`; dirty check uses `Array.from(Set)` to avoid downlevelIteration.
