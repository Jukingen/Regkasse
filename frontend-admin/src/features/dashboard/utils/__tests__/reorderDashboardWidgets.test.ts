import { describe, expect, it } from 'vitest';

import type { DashboardWidgetPreference } from '@/features/dashboard/types';
import { reorderDashboardWidgets } from '@/features/dashboard/utils/reorderDashboardWidgets';

function w(
  widgetId: string,
  order: number,
  isVisible: boolean
): DashboardWidgetPreference {
  return { widgetId, order, isVisible, settings: null };
}

describe('reorderDashboardWidgets', () => {
  it('moves a visible widget and rewrites order indices', () => {
    const widgets = [w('a', 0, true), w('b', 1, true), w('c', 2, true), w('hidden', 3, false)];

    const next = reorderDashboardWidgets(widgets, 'a', 'c');
    expect(next?.map((x) => x.widgetId)).toEqual(['b', 'c', 'a', 'hidden']);
    expect(next?.map((x) => x.order)).toEqual([0, 1, 2, 3]);
    expect(next?.find((x) => x.widgetId === 'hidden')?.isVisible).toBe(false);
  });

  it('returns null when ids match or are unknown', () => {
    const widgets = [w('a', 0, true), w('b', 1, true)];
    expect(reorderDashboardWidgets(widgets, 'a', 'a')).toBeNull();
    expect(reorderDashboardWidgets(widgets, 'missing', 'a')).toBeNull();
  });
});
