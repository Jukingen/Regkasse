import type { SidebarCatalogId } from '@/shared/adminSidebarRegistry';

/**
 * Sidebar "Lager" leaf and /inventory page surface. Next.js public env (build-time).
 * Products list Lager uses NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER separately.
 */
export function parseAdminShowInventoryNavEnv(raw: string | undefined): boolean {
  if (raw === undefined || raw === '') return true;
  const v = raw.trim().toLowerCase();
  return v !== 'false' && v !== '0' && v !== 'no' && v !== 'off';
}

export function isAdminInventoryNavEnabled(): boolean {
  return parseAdminShowInventoryNavEnv(process.env.NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV);
}

/** Remove `inventory` catalog leaf when the nav feature is off (sidebar + no accidental API prefetch). */
export function filterCatalogIdsForInventoryNav(
  catalogIds: readonly SidebarCatalogId[]
): SidebarCatalogId[] {
  if (isAdminInventoryNavEnabled()) return [...catalogIds];
  return catalogIds.filter((id) => id !== 'inventory');
}
