import { describe, expect, it } from 'vitest';
import { DASHBOARD_WIDGET_IDS } from '@/features/dashboard/types';
import { renderDashboardWidget } from '@/features/dashboard/widgets/widgetRegistry';

describe('renderDashboardWidget', () => {
    it('returns null for unknown widget id', () => {
        expect(renderDashboardWidget('unknown', { title: 'X' })).toBeNull();
    });

    it('renders today-sales widget', () => {
        const node = renderDashboardWidget(DASHBOARD_WIDGET_IDS.todaySales, {
            title: 'Heutiger Umsatz',
        });
        expect(node).not.toBeNull();
    });
});
