/**
 * Persist ordered export favorites per user in localStorage.
 */

import {
  DEFAULT_EXPORT_FAVORITE_IDS,
  isExportTypeId,
  type ExportTypeId,
} from '@/features/exports/exportTypeCatalog';
import { moveExportFavorite } from '@/features/exports/reorderExportFavorites';

const STORAGE_PREFIX = 'regkasse.admin.exportFavorites.v1:';

export type ExportFavoritesState = {
  /** Ordered favorite export type ids. */
  favoriteIds: ExportTypeId[];
};

function storageKey(userId: string): string {
  return `${STORAGE_PREFIX}${userId || 'anon'}`;
}

function sanitizeIds(ids: unknown): ExportTypeId[] {
  if (!Array.isArray(ids)) return [...DEFAULT_EXPORT_FAVORITE_IDS];
  const seen = new Set<string>();
  const out: ExportTypeId[] = [];
  for (const id of ids) {
    if (!isExportTypeId(id) || seen.has(id)) continue;
    seen.add(id);
    out.push(id);
  }
  return out;
}

export function loadExportFavorites(userId: string): ExportFavoritesState {
  if (typeof window === 'undefined') {
    return { favoriteIds: [...DEFAULT_EXPORT_FAVORITE_IDS] };
  }
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    if (!raw) return { favoriteIds: [...DEFAULT_EXPORT_FAVORITE_IDS] };
    const parsed = JSON.parse(raw) as { favoriteIds?: unknown };
    return { favoriteIds: sanitizeIds(parsed?.favoriteIds) };
  } catch {
    return { favoriteIds: [...DEFAULT_EXPORT_FAVORITE_IDS] };
  }
}

export function saveExportFavorites(userId: string, state: ExportFavoritesState): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(
      storageKey(userId),
      JSON.stringify({ favoriteIds: sanitizeIds(state.favoriteIds) })
    );
  } catch {
    // ignore quota / private mode
  }
}

export function toggleExportFavoriteId(
  favoriteIds: ExportTypeId[],
  id: ExportTypeId
): ExportTypeId[] {
  if (favoriteIds.includes(id)) {
    return favoriteIds.filter((x) => x !== id);
  }
  return [...favoriteIds, id];
}

/** Reorder favorites by moving activeId onto overId (same semantics as dashboard widgets). */
export function reorderExportFavoriteIds(
  favoriteIds: ExportTypeId[],
  activeId: string,
  overId: string
): ExportTypeId[] | null {
  return moveExportFavorite(favoriteIds, activeId, overId);
}
