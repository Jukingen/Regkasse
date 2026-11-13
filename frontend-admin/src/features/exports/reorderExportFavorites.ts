import { arrayMove } from '@dnd-kit/sortable';

import type { ExportTypeId } from '@/features/exports/exportTypeCatalog';

/**
 * Pure reorder helper (arrayMove) for unit tests and storage layer.
 */
export function moveExportFavorite(
  favoriteIds: ExportTypeId[],
  activeId: string,
  overId: string
): ExportTypeId[] | null {
  if (activeId === overId) return null;
  const oldIndex = favoriteIds.indexOf(activeId as ExportTypeId);
  const newIndex = favoriteIds.indexOf(overId as ExportTypeId);
  if (oldIndex < 0 || newIndex < 0) return null;
  return arrayMove(favoriteIds, oldIndex, newIndex);
}
