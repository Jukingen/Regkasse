/**
 * Mirrors backend `PermissionCatalogMetadata.GetGroupKey`: stable slug from catalog group display name.
 * Used to bucket catalog items and resolve `users.roleDrawer.groups.<slug>` labels per admin locale.
 *
 * Display order and group metadata: `permissionGroupRegistry.ts` (sidebar sync SSOT).
 */
import {
  PERMISSION_GROUP_ORDER as REGISTRY_GROUP_ORDER,
  type PermissionGroupKey,
} from '@/shared/auth/permissionGroupRegistry';

export function permissionCatalogGroupToSlug(group: string): string {
  const trimmed = group.trim();
  if (!trimmed) return 'other';
  const s = trimmed.replace(/ & /g, '_').replace(/ /g, '_').toLowerCase();
  return s || 'other';
}

/**
 * Sidebar-aligned display order for permission groups (Betrieb → RKSV → Sortiment → …).
 * Source: `PERMISSION_GROUPS` / `PERMISSION_GROUP_ORDER` in permissionGroupRegistry.
 */
export const PERMISSION_GROUP_ORDER: readonly string[] = REGISTRY_GROUP_ORDER;

export function comparePermissionGroupSlugs(
  slugA: string,
  slugB: string,
  labelA: string,
  labelB: string
): number {
  const idxA = PERMISSION_GROUP_ORDER.indexOf(slugA as PermissionGroupKey);
  const idxB = PERMISSION_GROUP_ORDER.indexOf(slugB as PermissionGroupKey);
  const orderA = idxA === -1 ? PERMISSION_GROUP_ORDER.length : idxA;
  const orderB = idxB === -1 ? PERMISSION_GROUP_ORDER.length : idxB;
  if (orderA !== orderB) return orderA - orderB;
  return labelA.localeCompare(labelB, undefined, { sensitivity: 'base' });
}
