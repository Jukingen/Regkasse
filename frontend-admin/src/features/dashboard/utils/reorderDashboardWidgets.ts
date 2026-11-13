import { arrayMove } from '@dnd-kit/sortable';

import type { DashboardWidgetPreference } from '@/features/dashboard/types';

/**
 * Apply a drag-end reorder among currently visible widgets.
 * Hidden widgets keep relative order after the visible block; `order` is rewritten 0..n-1.
 */
export function reorderDashboardWidgets(
  widgets: DashboardWidgetPreference[],
  activeId: string,
  overId: string
): DashboardWidgetPreference[] | null {
  if (activeId === overId) {
    return null;
  }

  const visible = widgets.filter((w) => w.isVisible).sort((a, b) => a.order - b.order);
  const ids = visible.map((w) => w.widgetId);
  const oldIndex = ids.indexOf(activeId);
  const newIndex = ids.indexOf(overId);
  if (oldIndex < 0 || newIndex < 0) {
    return null;
  }

  const reorderedVisible = arrayMove(visible, oldIndex, newIndex);
  const hidden = widgets.filter((w) => !w.isVisible).sort((a, b) => a.order - b.order);
  return [...reorderedVisible, ...hidden].map((w, index) => ({ ...w, order: index }));
}
