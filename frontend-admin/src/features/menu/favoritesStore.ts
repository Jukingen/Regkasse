'use client';

/**
 * Zustand store for ordered sidebar favorites. Persistence is localStorage
 * (`sidebar_favorites_{userId}`) — not auth tokens or React Query cache.
 */

import { create } from 'zustand';

import { MAX_SIDEBAR_FAVORITES } from '@/features/menu/menuConfig';
import {
  addFavoriteMenuKey,
  pruneInaccessibleFavorites,
  removeFavoriteMenuKey,
  reorderFavoriteMenuKeys,
  sanitizeFavoriteMenuKeys,
  type AddFavoriteResult,
} from '@/features/menu/sidebarFavoritesLogic';
import {
  saveSidebarFavorites,
  seedSidebarFavoritesIfMissing,
} from '@/features/menu/sidebarFavoritesStorage';

export type SidebarFavoritesState = {
  userId: string | null;
  favoriteIds: string[];
  hydrated: boolean;
  hydrate: (
    userId: string,
    role: string | null | undefined,
    visibleMenuKeys: ReadonlySet<string>
  ) => void;
  addFavorite: (menuKey: string) => AddFavoriteResult;
  removeFavorite: (menuKey: string) => void;
  reorderFavorites: (activeId: string, overId: string) => void;
  resetSession: () => void;
};

function persist(userId: string | null, ids: string[]): void {
  if (!userId) return;
  saveSidebarFavorites(userId, ids);
}

export const useFavoritesStore = create<SidebarFavoritesState>((set, get) => ({
  userId: null,
  favoriteIds: [],
  hydrated: false,

  hydrate: (userId, role, visibleMenuKeys) => {
    const current = get();
    if (current.hydrated && current.userId === userId) {
      const pruned = pruneInaccessibleFavorites(current.favoriteIds, visibleMenuKeys);
      if (pruned.length !== current.favoriteIds.length) {
        persist(userId, pruned);
        set({ favoriteIds: pruned });
      }
      return;
    }

    const loaded = seedSidebarFavoritesIfMissing(userId, role);
    const pruned = pruneInaccessibleFavorites(loaded, visibleMenuKeys);
    if (pruned.length !== loaded.length) {
      persist(userId, pruned);
    }
    set({ userId, favoriteIds: pruned, hydrated: true });
  },

  addFavorite: (menuKey) => {
    const { userId, favoriteIds } = get();
    const { ids, result } = addFavoriteMenuKey(favoriteIds, menuKey, MAX_SIDEBAR_FAVORITES);
    if (result === 'added') {
      persist(userId, ids);
      set({ favoriteIds: ids });
    }
    return result;
  },

  removeFavorite: (menuKey) => {
    const { userId, favoriteIds } = get();
    const ids = sanitizeFavoriteMenuKeys(removeFavoriteMenuKey(favoriteIds, menuKey));
    persist(userId, ids);
    set({ favoriteIds: ids });
  },

  reorderFavorites: (activeId, overId) => {
    const { userId, favoriteIds } = get();
    const next = reorderFavoriteMenuKeys(favoriteIds, activeId, overId);
    if (!next) return;
    persist(userId, next);
    set({ favoriteIds: next });
  },

  resetSession: () => {
    set({ userId: null, favoriteIds: [], hydrated: false });
  },
}));
