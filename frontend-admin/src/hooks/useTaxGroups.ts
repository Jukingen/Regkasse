'use client';

import { useQuery } from '@tanstack/react-query';

import {
  type TaxGroupAdmin,
  getTaxGroups,
  taxGroupsQueryKey,
} from '@/features/tax/api/taxGroups';

export function useTaxGroups(enabled = true) {
  return useQuery<TaxGroupAdmin[]>({
    queryKey: taxGroupsQueryKey,
    queryFn: getTaxGroups,
    enabled,
    staleTime: 60_000,
  });
}

/** Resolve a tax group for display: prefer id, then exact rate match among active groups. */
export function resolveTaxGroupForProduct(
  taxGroups: TaxGroupAdmin[] | undefined,
  opts: { taxGroupId?: string | null; taxRate?: number | null }
): TaxGroupAdmin | undefined {
  if (!taxGroups?.length) return undefined;
  if (opts.taxGroupId) {
    const byId = taxGroups.find((g) => g.id === opts.taxGroupId);
    if (byId) return byId;
  }
  if (opts.taxRate != null && Number.isFinite(Number(opts.taxRate))) {
    const rate = Number(opts.taxRate);
    return (
      taxGroups.find((g) => g.isActive && g.rate === rate) ??
      taxGroups.find((g) => g.rate === rate)
    );
  }
  return taxGroups.find((g) => g.isDefault && g.isActive);
}
