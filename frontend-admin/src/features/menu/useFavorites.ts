'use client';

import { useCallback, useEffect, useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useFavoritesStore } from '@/features/menu/favoritesStore';
import { MAX_SIDEBAR_FAVORITES } from '@/features/menu/menuConfig';
import { pruneInaccessibleFavorites } from '@/features/menu/sidebarFavoritesLogic';
import { useNotify } from '@/hooks/useNotify';

export type UseFavoritesOptions = {
  /** Permission-filtered menu keys (leaves + visible `grp-*` parents). */
  visibleMenuKeys: ReadonlySet<string>;
};

export function useFavorites(options: UseFavoritesOptions) {
  const { visibleMenuKeys } = options;
  const { user } = useAuth();
  const userId = user?.id ?? '';
  const role = user?.role ?? '';
  const notify = useNotify();

  const favoriteIds = useFavoritesStore((s) => s.favoriteIds);
  const hydrated = useFavoritesStore((s) => s.hydrated);
  const hydrate = useFavoritesStore((s) => s.hydrate);
  const addFavoriteRaw = useFavoritesStore((s) => s.addFavorite);
  const removeFavorite = useFavoritesStore((s) => s.removeFavorite);
  const reorderFavorites = useFavoritesStore((s) => s.reorderFavorites);

  useEffect(() => {
    if (!userId) return;
    hydrate(userId, role, visibleMenuKeys);
  }, [userId, role, visibleMenuKeys, hydrate]);

  const favorites = useMemo(
    () => pruneInaccessibleFavorites(favoriteIds, visibleMenuKeys),
    [favoriteIds, visibleMenuKeys]
  );

  const isFavorite = useCallback(
    (menuKey: string) => favoriteIds.includes(menuKey),
    [favoriteIds]
  );

  const addFavorite = useCallback(
    (menuKey: string) => {
      const result = addFavoriteRaw(menuKey);
      if (result === 'max') {
        notify.warning('adminShell.sidebar.favorites.maxReached');
      }
      return result;
    },
    [addFavoriteRaw, notify]
  );

  const toggleFavorite = useCallback(
    (menuKey: string) => {
      if (isFavorite(menuKey)) {
        removeFavorite(menuKey);
        return;
      }
      addFavorite(menuKey);
    },
    [addFavorite, isFavorite, removeFavorite]
  );

  return {
    favorites,
    favoriteIds,
    hydrated,
    maxCount: MAX_SIDEBAR_FAVORITES,
    isFavorite,
    addFavorite,
    removeFavorite,
    toggleFavorite,
    reorderFavorites,
  };
}
