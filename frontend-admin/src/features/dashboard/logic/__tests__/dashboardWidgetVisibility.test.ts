import { describe, expect, it } from 'vitest';
import {
    canShowDashboardWidget,
    filterDashboardCatalogByPermissions,
    filterDashboardLayoutByCatalog,
} from '@/features/dashboard/logic/dashboardWidgetVisibility';
import type { DashboardWidgetCatalogItem } from '@/features/dashboard/types';
import { PERMISSIONS } from '@/shared/auth/permissions';

const catalog: DashboardWidgetCatalogItem[] = [
    {
        widgetId: 'today-sales',
        title: 'Sales',
        description: '',
        requiredPermission: PERMISSIONS.REPORT_VIEW,
        defaultOrder: 0,
        defaultVisible: true,
        supportsAutoRefresh: true,
    },
    {
        widgetId: 'license-expiry',
        title: 'License',
        description: '',
        requiredPermission: PERMISSIONS.SETTINGS_MANAGE,
        defaultOrder: 1,
        defaultVisible: true,
        supportsAutoRefresh: true,
    },
    {
        widgetId: 'backup-status',
        title: 'Backup',
        description: '',
        requiredPermission: PERMISSIONS.SETTINGS_VIEW,
        defaultOrder: 2,
        defaultVisible: true,
        supportsAutoRefresh: true,
    },
    {
        widgetId: 'offline-system-status',
        title: 'Offline',
        description: '',
        requiredPermission: PERMISSIONS.PAYMENT_VIEW,
        defaultOrder: 3,
        defaultVisible: true,
        supportsAutoRefresh: true,
    },
];

describe('dashboardWidgetVisibility', () => {
    it('hides settings.manage widgets from managers', () => {
        const hasPermission = (p: string) =>
            p === PERMISSIONS.REPORT_VIEW ||
            p === PERMISSIONS.SETTINGS_VIEW ||
            p === PERMISSIONS.PAYMENT_VIEW;

        const filtered = filterDashboardCatalogByPermissions(catalog, hasPermission);
        expect(filtered.map((w) => w.widgetId)).toEqual([
            'today-sales',
            'backup-status',
            'offline-system-status',
        ]);
    });

    it('shows settings.manage widgets only when granted', () => {
        expect(
            canShowDashboardWidget(catalog[1], (p) => p === PERMISSIONS.SETTINGS_MANAGE),
        ).toBe(true);
        expect(canShowDashboardWidget(catalog[1], () => false)).toBe(false);
    });

    it('filters layout to allowed catalog ids', () => {
        const layout = [
            { widgetId: 'today-sales', order: 0, isVisible: true },
            { widgetId: 'license-expiry', order: 1, isVisible: true },
            { widgetId: 'backup-status', order: 2, isVisible: true },
        ];
        const allowed = catalog.filter((c) => c.widgetId !== 'license-expiry');
        const filtered = filterDashboardLayoutByCatalog(layout, allowed);
        expect(filtered.map((w) => w.widgetId)).toEqual(['today-sales', 'backup-status']);
    });
});
