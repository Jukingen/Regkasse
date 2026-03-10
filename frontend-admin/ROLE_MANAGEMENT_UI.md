# Role Management UI – Frontend Architecture Note

## Scope
Users page ("Benutzerverwaltung") extended with **Rollen verwalten**: a drawer to list roles, view/edit permissions per role, create role (existing modal), delete custom role. Backend contract is assumed ready (GET permissions-catalog, GET with-permissions, PUT/DELETE role).

## Presets
- **Data model:** `RolePreset { id, label, permissionKeys }` in `rolePresets.ts`. Preset apply replaces current draft with `getPresetKeysInCatalog(preset, catalogKeys)` (only keys present in catalog).
- **Semantics:** Apply updates only local draft state; no API call. User can then toggle checkboxes (manual override). Dirty = draft ≠ saved. Save persists; closing with dirty shows confirm.
- **Presets:** Kasa Operasyon, Muhasebe, Rapor Görüntüleme, Mağaza Yöneticisi.

## Affected Files
- `src/features/users/constants/rolePresets.ts` – Preset definitions and `getPresetKeysInCatalog`.
- `src/shared/auth/validateCatalogAlignment.ts` – Menu/route keys vs catalog; console warning when unknown.
- `src/features/users/api/roleManagementApi.ts` – New: DTOs and API calls for catalog, with-permissions, update, delete.
- `src/features/users/api/usersGateway.ts` – Query keys + re-exports for role management.
- `src/features/auth/constants/roles.ts` – `canDeleteRole`, `canEditRolePermissions` (SuperAdmin only).
- `src/shared/auth/usersPolicy.ts` – `canDeleteRole`, `canEditRolePermissions` in policy and actions.
- `src/features/users/hooks/useRolesWithPermissions.ts` – New hook.
- `src/features/users/hooks/usePermissionsCatalog.ts` – New hook.
- `src/features/users/components/RoleManagementDrawer.tsx` – New: two-panel drawer (role list | grouped permissions).
- `src/features/users/constants/copy.ts` – Role management copy (DE).
- `src/app/(protected)/users/page.tsx` – "Rollen verwalten" button, drawer state, mutations, invalidation.

## State
- **Server:** React Query for `rolesWithPermissions` and `permissionsCatalog`; enabled when drawer is open.
- **Local (drawer):** `selectedRoleName`, `draftPermissions` (Set). Dirty = draft ≠ saved for selected role.
- **Mutations:** `updateRolePermissions`, `deleteRole`; on success invalidate `rolesWithPermissionsQueryKey` and `rolesQueryKey`.

## UX Rules
- Role list sorted alphabetically; first role selected on open.
- System roles: delete disabled + tooltip; permissions read-only (no checkbox change).
- Dirty: confirm on close and on role switch; Save disabled when not dirty.
- Delete: confirm; after delete, select next role in list.
- "Neue Rolle" opens existing create-role modal (unchanged flow).

## Visibility
- "Rollen verwalten" button visible when `canCreateRole || canDeleteRole || canEditRolePermissions` (effectively SuperAdmin).
- Admin does not get these actions (policy + backend 403).

## Lint
- `RoleManagementDrawer`: `useEffect` depends on `selectedRole`; dirty check uses `Array.from(Set)` to avoid downlevelIteration.
