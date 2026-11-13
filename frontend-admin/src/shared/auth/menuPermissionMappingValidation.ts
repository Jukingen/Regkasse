/**
 * Dev-time / CI-friendly validation: every FA sidebar leaf should declare a catalog
 * `permission` (or be intentionally hidden) and stay aligned with `ROUTE_PERMISSIONS`.
 *
 * Runtime: call {@link logMenuPermissionMappingWarnings} once from Admin shell (dev only).
 * CI: `npm run verify:menu-permissions` (static script) remains the hard gate.
 */
import type { SidebarNavCatalogItem } from '@/shared/adminSidebarRegistry';
import { ROUTE_PERMISSIONS } from '@/shared/auth/routePermissions';

export type MenuPermissionMappingIssue = {
  menuKey: string;
  catalogId?: string;
  code: 'missing_catalog_permission' | 'missing_route_permission' | 'catalog_route_mismatch';
  message: string;
};

function normalizeRequired(value: string | readonly string[] | undefined): string[] | null {
  if (value === undefined) return null;
  if (Array.isArray(value)) {
    // Empty array = any authenticated (same sentinel as ROUTE_PERMISSIONS / ANY_AUTHENTICATED_PERMISSION).
    return [...value].map(String).sort();
  }
  return [String(value)].sort();
}

function hasOverlap(a: string[], b: string[]): boolean {
  if (a.length === 0 && b.length === 0) return true;
  const setB = new Set(b);
  return a.some((x) => setB.has(x));
}

/**
 * Validate sidebar catalog leaves against route permission gates.
 * Hidden leaves still require a ROUTE_PERMISSIONS entry when deep-linked.
 */
export function validateMenuPermissionMappings(
  catalog: Readonly<Record<string, SidebarNavCatalogItem>>,
  routePermissions: Record<string, string | readonly string[] | undefined> = ROUTE_PERMISSIONS
): MenuPermissionMappingIssue[] {
  const issues: MenuPermissionMappingIssue[] = [];

  for (const [catalogId, item] of Object.entries(catalog)) {
    const menuKey = item.menuKey;
    const routeRequired = routePermissions[menuKey];
    const catalogRequired = normalizeRequired(item.permission);

    if (routeRequired === undefined) {
      issues.push({
        menuKey,
        catalogId,
        code: 'missing_route_permission',
        message: `Sidebar leaf "${catalogId}" (${menuKey}) has no ROUTE_PERMISSIONS entry`,
      });
    }

    if (!item.sidebarHidden && catalogRequired == null) {
      issues.push({
        menuKey,
        catalogId,
        code: 'missing_catalog_permission',
        message: `Visible sidebar leaf "${catalogId}" (${menuKey}) is missing catalog.permission`,
      });
    }

    if (catalogRequired != null && routeRequired !== undefined) {
      const routeNorm = normalizeRequired(routeRequired);
      if (routeNorm && !hasOverlap(catalogRequired, routeNorm)) {
        issues.push({
          menuKey,
          catalogId,
          code: 'catalog_route_mismatch',
          message: `Catalog permission [${catalogRequired.join(', ') || '(any-auth)'}] does not overlap ROUTE_PERMISSIONS [${routeNorm.join(', ') || '(any-auth)'}] for ${menuKey}`,
        });
      }
    }
  }

  return issues;
}

/** Console.warn each issue once (development / debug builds). */
export function logMenuPermissionMappingWarnings(
  catalog: Readonly<Record<string, SidebarNavCatalogItem>>,
  routePermissions: Record<string, string | readonly string[] | undefined> = ROUTE_PERMISSIONS
): MenuPermissionMappingIssue[] {
  const issues = validateMenuPermissionMappings(catalog, routePermissions);
  for (const issue of issues) {
    // eslint-disable-next-line no-console -- intentional mapping audit in development
    console.warn(`[menu-permission-mapping] ${issue.code}: ${issue.message}`);
  }
  return issues;
}
