/**
 * Persist ordered sidebar favorites per user in localStorage.
 * Key: `sidebar_favorites_{userId}` — survives logout/login on the same browser.
 */

import {
  getDefaultFavoriteMenuKeys,
  sidebarFavoritesStorageKey,
} from '@/features/menu/menuConfig';
import { sanitizeFavoriteMenuKeys } from '@/features/menu/sidebarFavoritesLogic';

export type SidebarFavoritesPersisted = {
  favoriteIds: string[];
};

/** `null` means no row yet — caller should seed role defaults. */
export function loadSidebarFavorites(userId: string): SidebarFavoritesPersisted | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(sidebarFavoritesStorageKey(userId));
    if (raw == null) return null;
    const parsed: unknown = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return { favoriteIds: sanitizeFavoriteMenuKeys(parsed) };
    }
    if (parsed && typeof parsed === 'object' && 'favoriteIds' in parsed) {
      return {
        favoriteIds: sanitizeFavoriteMenuKeys((parsed as { favoriteIds?: unknown }).favoriteIds),
      };
    }
    return { favoriteIds: [] };
  } catch {
    return { favoriteIds: [] };
  }
}

export function saveSidebarFavorites(userId: string, favoriteIds: readonly string[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(
      sidebarFavoritesStorageKey(userId),
      JSON.stringify(sanitizeFavoriteMenuKeys(favoriteIds))
    );
  } catch {
    // quota / private mode
  }
}

export function seedSidebarFavoritesIfMissing(
  userId: string,
  role: string | null | undefined
): string[] {
  const existing = loadSidebarFavorites(userId);
  if (existing) return existing.favoriteIds;
  const seeded = sanitizeFavoriteMenuKeys(getDefaultFavoriteMenuKeys(role));
  saveSidebarFavorites(userId, seeded);
  return seeded;
}
