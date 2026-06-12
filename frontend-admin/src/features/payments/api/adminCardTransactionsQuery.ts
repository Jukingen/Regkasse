import { keepPreviousData, useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';

export type AdminCardTransactionRow = {
  id: string;
  amount: number;
  currency: string;
  status: string;
  gatewayProvider: string;
  transactionId?: string | null;
  cardBrand?: string | null;
  lastFourDigits?: string | null;
  errorMessage?: string | null;
  cashRegisterId: string;
  cashRegisterLabel?: string | null;
  paymentDetailsId?: string | null;
  receiptNumber?: string | null;
  createdAtUtc: string;
  confirmedAtUtc?: string | null;
  refundedAmount?: number | null;
};

export type AdminCardTransactionListResponse = {
  items: AdminCardTransactionRow[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
};

export type AdminCardTransactionListParams = {
  fromUtc?: string;
  toUtc?: string;
  status?: string;
  cashRegisterId?: string;
  pageNumber?: number;
  pageSize?: number;
};

export async function fetchAdminCardTransactions(
  params: AdminCardTransactionListParams,
  signal?: AbortSignal,
): Promise<AdminCardTransactionListResponse> {
  return customInstance<AdminCardTransactionListResponse>({
    url: '/api/admin/card-transactions',
    method: 'GET',
    params,
    paramsSerializer: { indexes: null },
    signal,
  });
}

export function adminCardTransactionsQueryKey(params: AdminCardTransactionListParams) {
  return ['/api/admin/card-transactions', params] as const;
}

export function useAdminCardTransactionsList<TData = AdminCardTransactionListResponse>(
  params: AdminCardTransactionListParams,
  options?: {
    query?: Partial<UseQueryOptions<AdminCardTransactionListResponse, unknown, TData>>;
  },
) {
  const { query: queryOptions } = options ?? {};
  return useQuery({
    queryKey: adminCardTransactionsQueryKey(params),
    queryFn: ({ signal }) => fetchAdminCardTransactions(params, signal),
    placeholderData: keepPreviousData,
    ...queryOptions,
  });
}
