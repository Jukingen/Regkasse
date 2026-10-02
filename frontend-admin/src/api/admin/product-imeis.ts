import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export type AdminProductImeiStatus = 'InStock' | 'Sold' | 'Returned';

export interface AdminProductImei {
  id: string;
  productId: string;
  imei: string;
  status: AdminProductImeiStatus;
  soldPaymentId?: string | null;
  warrantyMonths: number;
  createdAtUtc: string;
  soldAtUtc?: string | null;
}

function productImeisUrl(productId: string): string {
  return `/api/admin/products/${encodeURIComponent(productId)}/imeis`;
}

export const adminProductImeiQueryKeys = {
  all: ['admin', 'product-imeis'] as const,
  list: (productId: string) => [...adminProductImeiQueryKeys.all, productId] as const,
};

export function getAdminProductImeis(
  productId: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminProductImei[]> {
  return customInstance<AdminProductImei[]>(
    { url: productImeisUrl(productId), method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminProductImei[]>(res));
}

export function useAdminProductImeis(
  productId: string | undefined,
  enabled = true,
  options?: Partial<UseQueryOptions<AdminProductImei[], Error, AdminProductImei[]>>
): UseQueryResult<AdminProductImei[], Error> {
  return useQuery({
    queryKey: adminProductImeiQueryKeys.list(productId ?? ''),
    queryFn: ({ signal }) => getAdminProductImeis(productId!, undefined, signal),
    enabled: enabled && Boolean(productId),
    ...options,
  });
}
