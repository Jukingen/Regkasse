/**
 * Ürün bazında modifier gruplarını cache'li çeker. POS satırında "Extras" alanının
 * gösterilip gösterilmeyeceği ve bottom sheet içeriği için kullanılır.
 */
import { useState, useEffect, useCallback } from 'react';
import {
  getProductModifierGroups,
  type ModifierGroupDto,
} from '../services/api/productModifiersService';

export interface UseProductModifierGroupsResult {
  groups: ModifierGroupDto[];
  loading: boolean;
  hasModifiers: boolean;
  refetch: () => Promise<void>;
}

export function useProductModifierGroups(productId: string | null): UseProductModifierGroupsResult {
  const [groups, setGroups] = useState<ModifierGroupDto[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchGroups = useCallback(async () => {
    if (!productId) {
      setGroups([]);
      return;
    }
    setLoading(true);
    try {
      const data = await getProductModifierGroups(productId);
      setGroups(data);
    } catch {
      setGroups([]);
    } finally {
      setLoading(false);
    }
  }, [productId]);

  useEffect(() => {
    if (!productId) {
      setGroups([]);
      setLoading(false);
      return;
    }
    fetchGroups();
  }, [productId, fetchGroups]);

  const hasModifiers = groups.some((g) => (g.modifiers?.length ?? 0) > 0);

  return { groups, loading, hasModifiers, refetch: fetchGroups };
}
