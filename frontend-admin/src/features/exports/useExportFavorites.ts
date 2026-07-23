'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import {
  EXPORT_TYPE_CATALOG,
  getExportTypeById,
  type ExportTypeDef,
  type ExportTypeId,
} from '@/features/exports/exportTypeCatalog';
import {
  loadExportFavorites,
  reorderExportFavoriteIds,
  saveExportFavorites,
  toggleExportFavoriteId,
} from '@/features/exports/exportFavoritesStorage';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { usePermissions } from '@/shared/auth/usePermissions';

export function useExportFavorites() {
  const { user } = useAuth();
  const userId = user?.id ?? 'anon';
  const { hasPermission } = usePermissions();
  const [favoriteIds, setFavoriteIds] = useState<ExportTypeId[]>([]);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    setFavoriteIds(loadExportFavorites(userId).favoriteIds);
    setHydrated(true);
  }, [userId]);

  const persist = useCallback(
    (next: ExportTypeId[]) => {
      setFavoriteIds(next);
      saveExportFavorites(userId, { favoriteIds: next });
    },
    [userId]
  );

  const visibleCatalog = useMemo(
    () => EXPORT_TYPE_CATALOG.filter((e) => hasPermission(e.permission)),
    [hasPermission]
  );

  const favorites = useMemo(() => {
    return favoriteIds
      .map((id) => getExportTypeById(id))
      .filter((e): e is ExportTypeDef => Boolean(e) && hasPermission(e.permission));
  }, [favoriteIds, hasPermission]);

  const isFavorite = useCallback(
    (id: ExportTypeId) => favoriteIds.includes(id),
    [favoriteIds]
  );

  const toggleFavorite = useCallback(
    (id: ExportTypeId) => {
      persist(toggleExportFavoriteId(favoriteIds, id));
    },
    [favoriteIds, persist]
  );

  const reorderFavorites = useCallback(
    (activeId: string, overId: string) => {
      const next = reorderExportFavoriteIds(favoriteIds, activeId, overId);
      if (next) persist(next);
    },
    [favoriteIds, persist]
  );

  return {
    hydrated,
    favoriteIds,
    favorites,
    visibleCatalog,
    isFavorite,
    toggleFavorite,
    reorderFavorites,
  };
}
