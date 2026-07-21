import type {
  DashboardWidgetCatalogItem,
  DashboardWidgetPreference,
} from '@/features/dashboard/types';
import { PERMISSIONS } from '@/shared/auth/permissions';

type PermissionChecker = (permission: string) => boolean;

/**
 * Client-side catalog filter — mirrors backend `DashboardWidgetCatalog.FilterByPermissions`
 * and hides platform-only widgets (settings.manage) from tenant operators.
 */
export function filterDashboardCatalogByPermissions(
  catalog: DashboardWidgetCatalogItem[],
  hasPermission: PermissionChecker
): DashboardWidgetCatalogItem[] {
  return catalog.filter((item) => canShowDashboardWidget(item, hasPermission));
}

export function canShowDashboardWidget(
  item: DashboardWidgetCatalogItem,
  hasPermission: PermissionChecker
): boolean {
  if (item.requiredPermission === PERMISSIONS.SETTINGS_MANAGE) {
    return hasPermission(PERMISSIONS.SETTINGS_MANAGE);
  }
  return hasPermission(item.requiredPermission);
}

export function filterDashboardLayoutByCatalog(
  layout: DashboardWidgetPreference[],
  catalog: DashboardWidgetCatalogItem[]
): DashboardWidgetPreference[] {
  const allowedIds = new Set(catalog.map((c) => c.widgetId));
  return layout.filter((w) => allowedIds.has(w.widgetId));
}
