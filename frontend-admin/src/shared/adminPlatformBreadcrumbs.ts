/**
 * Platform-admin breadcrumb helpers aligned with sidebar IA groups
 * (Verwaltung / Sicherheit & TSE / Deployment & System / Monitoring & Logs).
 */
import {
  buildAdminBreadcrumbs,
  type AdminBreadcrumbItem,
} from '@/shared/adminShellLabels';
import {
  ADMIN_SIDEBAR_GROUP_KEYS,
  ADMIN_SIDEBAR_GROUP_ROUTES,
  normalizeAdminPathname,
} from '@/shared/adminSidebarNavigation';
import { SIDEBAR_GROUP_META, type SidebarGroupId } from '@/shared/adminSidebarRegistry';

/** Sidebar groups introduced for Super Admin platform IA. */
export type PlatformAdminBreadcrumbGroup =
  | 'administration'
  | 'securityTse'
  | 'deploymentSystem'
  | 'monitoringLogs';

/** Primary leaf hub for each group (used as parent crumb link). */
export const PLATFORM_ADMIN_GROUP_HUB_HREF: Record<PlatformAdminBreadcrumbGroup, string> = {
  administration: '/admin/access',
  securityTse: '/admin/tse-management',
  deploymentSystem: '/admin/deployments',
  monitoringLogs: '/admin/monitoring',
};

const PLATFORM_GROUP_ROUTE_KEYS: Record<PlatformAdminBreadcrumbGroup, string> = {
  administration: ADMIN_SIDEBAR_GROUP_KEYS.admin,
  securityTse: ADMIN_SIDEBAR_GROUP_KEYS.securityTse,
  deploymentSystem: ADMIN_SIDEBAR_GROUP_KEYS.deploymentSystem,
  monitoringLogs: ADMIN_SIDEBAR_GROUP_KEYS.monitoringLogs,
};

/** Prefer more specific groups when paths could theoretically overlap. */
const PLATFORM_GROUP_RESOLVE_ORDER: readonly PlatformAdminBreadcrumbGroup[] = [
  'securityTse',
  'deploymentSystem',
  'monitoringLogs',
  'administration',
];

export function isPlatformAdminBreadcrumbGroup(
  group: SidebarGroupId
): group is PlatformAdminBreadcrumbGroup {
  return (
    group === 'administration' ||
    group === 'securityTse' ||
    group === 'deploymentSystem' ||
    group === 'monitoringLogs'
  );
}

export function adminSidebarGroupCrumb(
  group: PlatformAdminBreadcrumbGroup,
  t: (key: string) => string
): AdminBreadcrumbItem {
  return {
    title: t(SIDEBAR_GROUP_META[group].labelKey),
    href: PLATFORM_ADMIN_GROUP_HUB_HREF[group],
  };
}

/**
 * Resolve which platform IA group a pathname belongs to (sidebar route map).
 */
export function resolvePlatformAdminBreadcrumbGroup(
  pathname: string | null | undefined
): PlatformAdminBreadcrumbGroup | null {
  const p = normalizeAdminPathname(pathname);
  if (!p) return null;

  for (const group of PLATFORM_GROUP_RESOLVE_ORDER) {
    const routes = ADMIN_SIDEBAR_GROUP_ROUTES[PLATFORM_GROUP_ROUTE_KEYS[group]] ?? [];
    if (routes.some((r) => p === r || p.startsWith(`${r}/`))) {
      return group;
    }
  }
  return null;
}

/**
 * Overview → group → leaf trail for platform-admin pages.
 */
export function buildPlatformAdminBreadcrumbs(
  t: (key: string) => string,
  group: PlatformAdminBreadcrumbGroup,
  leaf: AdminBreadcrumbItem | readonly AdminBreadcrumbItem[]
): AdminBreadcrumbItem[] {
  const leaves = Array.isArray(leaf) ? [...leaf] : [leaf];
  return buildAdminBreadcrumbs(t, [adminSidebarGroupCrumb(group, t), ...leaves]);
}
