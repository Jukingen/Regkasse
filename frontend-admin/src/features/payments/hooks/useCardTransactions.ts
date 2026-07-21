'use client';

import { useMemo, useState } from 'react';

import {
  type AdminCardTransactionListParams,
  type AdminCardTransactionRow,
  useAdminCardTransactionsList,
} from '@/features/payments/api/adminCardTransactionsQuery';

export type UseCardTransactionsResult = {
  data: AdminCardTransactionRow[];
  isLoading: boolean;
  isFetching: boolean;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  setPageNumber: (page: number) => void;
  setPageSize: (size: number) => void;
  setFilters: (patch: Partial<AdminCardTransactionListParams>) => void;
  refetch: () => void;
};

export function useCardTransactions(
  initialParams?: Partial<AdminCardTransactionListParams>
): UseCardTransactionsResult {
  const [params, setParamsState] = useState<AdminCardTransactionListParams>({
    pageNumber: 1,
    pageSize: 50,
    ...initialParams,
  });

  const listParams = useMemo(
    () => ({
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 50,
      status: params.status,
      cashRegisterId: params.cashRegisterId,
      fromUtc: params.fromUtc,
      toUtc: params.toUtc,
    }),
    [params]
  );

  const query = useAdminCardTransactionsList(listParams);

  const setPageNumber = (pageNumber: number) => {
    setParamsState((prev) => ({ ...prev, pageNumber }));
  };

  const setPageSize = (pageSize: number) => {
    setParamsState((prev) => ({ ...prev, pageSize, pageNumber: 1 }));
  };

  const setFilters = (patch: Partial<AdminCardTransactionListParams>) => {
    setParamsState((prev) => ({ ...prev, ...patch, pageNumber: 1 }));
  };

  return {
    data: query.data?.items ?? [],
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    totalCount: query.data?.totalCount ?? 0,
    pageNumber: listParams.pageNumber,
    pageSize: listParams.pageSize,
    setPageNumber,
    setPageSize,
    setFilters,
    refetch: () => {
      void query.refetch();
    },
  };
}
