'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type ModifierGroupDto,
  getModifierGroups,
  getProductModifierGroups,
} from '@/lib/api/modifierGroups';
import { queryCacheDynamic } from '@/lib/query/queryCachePolicy';

/** Shared with `/modifier-groups` page so list mutations invalidate this cache. */
export const modifierGroupsQueryKey = ['modifier-groups'] as const;

export function productModifierGroupsQueryKey(productId: string) {
  return ['admin', 'products', productId, 'modifier-groups'] as const;
}

export function modifierGroupIdsFromAssigned(groups: ModifierGroupDto[]): string[] {
  return groups
    .map((g) => {
      const raw = g.id ?? (g as { Id?: string }).Id;
      return raw == null ? '' : String(raw);
    })
    .filter(Boolean);
}

/** Full add-on group catalog (shared `/api/modifier-groups`). */
export function useModifierGroupsList(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: modifierGroupsQueryKey,
    queryFn: () => getModifierGroups(),
    enabled: options?.enabled !== false,
    ...queryCacheDynamic,
  });
}

/** Modifier groups already assigned to a product (admin API). */
export function useProductModifierGroups(
  productId: string | undefined,
  options?: { enabled?: boolean }
) {
  const enabled = Boolean(productId) && options?.enabled !== false;
  return useQuery({
    queryKey: productId
      ? productModifierGroupsQueryKey(productId)
      : (['admin', 'products', 'none', 'modifier-groups'] as const),
    queryFn: ({ signal }) => getProductModifierGroups(productId!, signal),
    enabled,
    ...queryCacheDynamic,
  });
}
