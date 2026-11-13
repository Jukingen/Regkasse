'use client';

import { useMemo } from 'react';

import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import {
  type PastMissingMonatsbelegEntry,
  collectPastMissingMonatsbelege,
  countPastMissingMonatsbelege,
} from '@/features/rksv/utils/monatsbelegMissingMonths';

type UsePastMissingMonatsbelegeOptions = {
  enabled?: boolean;
};

export function usePastMissingMonatsbelege(options?: UsePastMissingMonatsbelegeOptions) {
  const enabled = options?.enabled ?? true;
  const query = useMonatsbelegStatus({ enabled });

  const pastMissingEntries = useMemo(
    (): PastMissingMonatsbelegEntry[] => collectPastMissingMonatsbelege(query.data),
    [query.data]
  );

  const otherMissingCount = useMemo(() => countPastMissingMonatsbelege(query.data), [query.data]);

  return {
    ...query,
    pastMissingEntries,
    otherMissingCount,
  };
}
