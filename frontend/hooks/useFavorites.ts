import { useCallback, useEffect, useState } from 'react';

import {
  cashierFavoriteService,
  type CashierFavoriteDto,
} from '../services/api/cashierFavoriteService';

export type FavoriteItem = CashierFavoriteDto;

export function useFavorites() {
  const [favorites, setFavorites] = useState<FavoriteItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const rows = await cashierFavoriteService.listDtos();
      const items = [...rows].sort((a, b) => a.sortOrder - b.sortOrder);
      setFavorites(items);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Favoriten konnten nicht geladen werden';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const addFavorite = useCallback(
    async (productId: string) => {
      await cashierFavoriteService.add(productId);
      await refresh();
    },
    [refresh]
  );

  const removeFavorite = useCallback(async (favoriteId: string) => {
    await cashierFavoriteService.remove(favoriteId);
    setFavorites((prev) => prev.filter((f) => f.id !== favoriteId));
  }, []);

  const reorderFavorites = useCallback(
    async (orderIds: string[]) => {
      await cashierFavoriteService.reorder(orderIds);
      await refresh();
    },
    [refresh]
  );

  const isFavorite = useCallback(
    (productId: string) => favorites.some((f) => f.productId === productId),
    [favorites]
  );

  const toggleFavorite = useCallback(
    async (productId: string) => {
      const existing = favorites.find((f) => f.productId === productId);
      if (existing) {
        await removeFavorite(existing.id);
      } else {
        await addFavorite(productId);
      }
    },
    [favorites, addFavorite, removeFavorite]
  );

  return {
    favorites,
    loading,
    error,
    refresh,
    addFavorite,
    removeFavorite,
    reorderFavorites,
    isFavorite,
    toggleFavorite,
  };
}
