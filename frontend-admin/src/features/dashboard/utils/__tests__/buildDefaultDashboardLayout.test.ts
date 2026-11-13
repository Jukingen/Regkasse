import { describe, expect, it } from 'vitest';

import type { DashboardWidgetCatalogItem } from '@/features/dashboard/types';
import { DASHBOARD_WIDGET_IDS } from '@/features/dashboard/types';
import { buildDefaultDashboardLayout } from '@/features/dashboard/utils/buildDefaultDashboardLayout';

function catalogItem(
  widgetId: string,
  defaultOrder: number,
  defaultVisible = true
): DashboardWidgetCatalogItem {
  return {
    widgetId,
    title: widgetId,
    description: '',
    requiredPermission: 'report.view',
    defaultOrder,
    defaultVisible,
    supportsAutoRefresh: true,
  };
}

describe('buildDefaultDashboardLayout', () => {
  it('orders by defaultOrder and applies default visibility', () => {
    const catalog = [
      catalogItem(DASHBOARD_WIDGET_IDS.todaySales, 10),
      catalogItem(DASHBOARD_WIDGET_IDS.managerKpiStrip, 1),
      catalogItem(DASHBOARD_WIDGET_IDS.managerLicenseStatus, 0),
      catalogItem(DASHBOARD_WIDGET_IDS.managerHospitalityLinks, 8, false),
    ];

    const layout = buildDefaultDashboardLayout(catalog);
    expect(layout.map((w) => w.widgetId)).toEqual([
      DASHBOARD_WIDGET_IDS.managerLicenseStatus,
      DASHBOARD_WIDGET_IDS.managerKpiStrip,
      DASHBOARD_WIDGET_IDS.managerHospitalityLinks,
      DASHBOARD_WIDGET_IDS.todaySales,
    ]);
    expect(layout.map((w) => w.order)).toEqual([0, 1, 2, 3]);
    expect(layout.find((w) => w.widgetId === DASHBOARD_WIDGET_IDS.managerHospitalityLinks)?.isVisible).toBe(
      false
    );
  });

  it('seeds top-selling-products period setting', () => {
    const layout = buildDefaultDashboardLayout([
      catalogItem(DASHBOARD_WIDGET_IDS.topSellingProducts, 0),
    ]);
    expect(layout[0]?.settings).toEqual({ period: 'today' });
  });
});
