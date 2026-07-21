/**
 * Mirrors backend `PermissionCatalogMetadata.GetGroupKey`: stable slug from catalog group display name.
 * Used to bucket catalog items and resolve `users.roleDrawer.groups.<slug>` labels per admin locale.
 */
export function permissionCatalogGroupToSlug(group: string): string {
  const trimmed = group.trim();
  if (!trimmed) return 'other';
  const s = trimmed.replace(/ & /g, '_').replace(/ /g, '_').toLowerCase();
  return s || 'other';
}
