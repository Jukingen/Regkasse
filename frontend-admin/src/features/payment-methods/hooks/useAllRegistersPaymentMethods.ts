'use client';

import { useQueries } from '@tanstack/react-query';
import { useMemo } from 'react';

import {
  type PaymentMethodDefinitionAdmin,
  adminPaymentMethodDefinitionsQueryKeys,
  getAdminPaymentMethodDefinitions,
} from '@/api/admin/payment-method-definitions';

export function useAllRegistersPaymentMethods(registerIds: string[], enabled: boolean) {
  const queries = useQueries({
    queries: registerIds.map((registerId) => ({
      queryKey: adminPaymentMethodDefinitionsQueryKeys.list(registerId),
      queryFn: ({ signal }: { signal?: AbortSignal }) =>
        getAdminPaymentMethodDefinitions(registerId, undefined, signal),
      enabled: enabled && Boolean(registerId),
      staleTime: 30_000,
    })),
  });

  const isLoading = queries.some((q) => q.isLoading);
  const isFetching = queries.some((q) => q.isFetching);
  const error = queries.find((q) => q.error)?.error ?? null;

  const methodsByRegisterId = useMemo(() => {
    const map: Record<string, PaymentMethodDefinitionAdmin[]> = {};
    registerIds.forEach((id, index) => {
      map[id] = queries[index]?.data ?? [];
    });
    return map;
  }, [registerIds, queries]);

  const refetchAll = async () => {
    await Promise.all(queries.map((q) => q.refetch()));
  };

  return { methodsByRegisterId, isLoading, isFetching, error, refetchAll };
}
