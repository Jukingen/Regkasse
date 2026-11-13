'use client';

import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import {
  getCurrentTaxRegulation,
  isTaxRateValidForRegulation,
  taxRegulationCurrentQueryKey,
  type TaxRegulation,
} from '@/features/tax/api/taxRegulations';

export type TaxRegulationWithValidator = TaxRegulation & {
  isTaxRateValid: (rate: number | null | undefined) => boolean;
};

export function useCurrentTaxRegulation(enabled = true) {
  const query = useQuery({
    queryKey: taxRegulationCurrentQueryKey,
    queryFn: getCurrentTaxRegulation,
    enabled,
    staleTime: 5 * 60_000,
  });

  const regulation = useMemo((): TaxRegulationWithValidator | undefined => {
    if (!query.data) return undefined;
    return {
      ...query.data,
      isTaxRateValid: (rate) => isTaxRateValidForRegulation(query.data, rate),
    };
  }, [query.data]);

  return {
    ...query,
    data: regulation,
  };
}
