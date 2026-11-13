import { useCallback, useEffect, useMemo, useState } from 'react';

import { useAuth } from '../contexts/AuthContext';
import {
  cashierFavoriteService,
  type CashierFavoriteDto,
} from '../services/api/cashierFavoriteService';
import { ROLES } from '../types/auth';

export type FavoriteItem = CashierFavoriteDto;

const FAVORITES_ALLOWED_ROLES = new Set<string>([ROLES.Cashier, ROLES.Manager]);

function canUseFavorites(role?: string | null, roles?: string[] | null): boolean {
  if (role && FAVORITES_ALLOWED_ROLES.has(role)) {
    return true;
  }
  return roles?.some((r) => FAVORITES_ALLOWED_ROLES.has(r)) ?? false;
}

function isForbiddenError(e: unknown): boolean {
  if (typeof e !== 'object' || e === null) {
    return false;
  }
  const err = e as { status?: number; response?: { status?: number } };
  return err.status === 403 || err.response?.status === 403;
}

export function useFavorites() {
  const { user } = useAuth();
  const [favorites, setFavorites] = useState<FavoriteItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const shouldFetch = useMemo(
    () => canUseFavorites(user?.role, user?.roles),
    [user?.role, user?.roles]
  );

  const refresh = useCallback(async () => {
    if (!shouldFetch) {
      setFavorites([]);
      setError(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const rows = await cashierFavoriteService.listDtos();
      const items = [...rows].sort((a, b) => a.sortOrder - b.sortOrder);
      setFavorites(items);
    } catch (e: unknown) {
      if (isForbiddenError(e)) {
        return;
      }
      const msg = e instanceof Error ? e.message : 'Favoriten konnten nicht geladen werden';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [shouldFetch]);

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
