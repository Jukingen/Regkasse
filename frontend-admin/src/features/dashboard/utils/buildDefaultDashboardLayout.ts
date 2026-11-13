import type {
  DashboardWidgetCatalogItem,
  DashboardWidgetPreference,
} from '@/features/dashboard/types';
import { DASHBOARD_WIDGET_IDS } from '@/features/dashboard/types';

/**
 * Client-side default layout from catalog (mirrors backend `BuildDefaultLayout`).
 * Used for "Reset layout" without an extra API round-trip.
 */
export function buildDefaultDashboardLayout(
  catalog: DashboardWidgetCatalogItem[]
): DashboardWidgetPreference[] {
  return [...catalog]
    .sort((a, b) => a.defaultOrder - b.defaultOrder)
    .map((item, index) => ({
      widgetId: item.widgetId,
      order: index,
      isVisible: item.defaultVisible,
      settings:
        item.widgetId === DASHBOARD_WIDGET_IDS.topSellingProducts
          ? { period: 'today' }
          : item.widgetId === DASHBOARD_WIDGET_IDS.paymentTrends
            ? { period: 'Daily' }
            : null,
    }));
}
