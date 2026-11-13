/**
 * Permission implication map for menu visibility + permission UI.
 *
 * Canonical keys only (`user.manage`, `cash_register.manage`, …) — mirrors
 * `backend/Authorization/PermissionImplication.cs` via `permissionImplication.ts`.
 *
 * There is **no** `backup.view` / `users.view` / `cashregister.view` in the catalog.
 * `settings.manage` implies `settings.view` + `backup.manage` (via parent map).
 *
 * `hasPermission` / `permissionImplied` already honour this map for menu checks
 * (`useMenuPermissions`, `isMenuItemAllowed`).
 */
import {
  HOLDER_TO_IMPLIED_READS,
  PARENT_TO_CHILDREN,
  findImplicationSources,
  isPermissionImpliedOnly,
  permissionImplied,
} from './permissionImplication';

function mergeImplicationLists(
  a: readonly string[] | undefined,
  b: readonly string[] | undefined
): string[] {
  return [...new Set([...(a ?? []), ...(b ?? [])])].sort((x, y) => x.localeCompare(y));
}

/**
 * Holder permission → permissions it satisfies (manage→view, composites→children, …).
 * Shape matches the product sketch; values are backend-aligned.
 */
export const PERMISSION_IMPLICATIONS: Readonly<Record<string, readonly string[]>> = (() => {
  const map: Record<string, string[]> = {};

  for (const [holder, implied] of Object.entries(HOLDER_TO_IMPLIED_READS)) {
    map[holder] = mergeImplicationLists(map[holder], implied);
  }
  for (const [holder, children] of Object.entries(PARENT_TO_CHILDREN)) {
    map[holder] = mergeImplicationLists(map[holder], children);
  }

  // Documented aliases for common menu gates (still canonical keys).
  map['digital.orders.manage'] = mergeImplicationLists(map['digital.orders.manage'], [
    'digital.orders.view',
  ]);

  return Object.freeze(
    Object.fromEntries(
      Object.entries(map).map(([k, v]) => [k, Object.freeze(v) as readonly string[]])
    )
  ) as Readonly<Record<string, readonly string[]>>;
})();

/** Permissions satisfied when `holder` is granted (empty if none). */
export function getImpliedPermissions(holder: string): readonly string[] {
  return PERMISSION_IMPLICATIONS[holder] ?? [];
}

export {
  findImplicationSources,
  isPermissionImpliedOnly,
  permissionImplied,
};
