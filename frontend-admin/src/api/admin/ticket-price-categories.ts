import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';
import { queryCacheStatic } from '@/lib/query/queryCachePolicy';

export interface AdminTicketPriceCategory {
  id: string;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface UpsertTicketPriceCategoryBody {
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export const adminTicketPriceCategoryQueryKeys = {
  all: ['admin', 'ticket-price-categories'] as const,
};

export function getAdminTicketPriceCategories(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminTicketPriceCategory[]> {
  return customInstance<AdminTicketPriceCategory[]>(
    { url: '/api/admin/ticket-price-categories', method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminTicketPriceCategory[]>(res));
}

export function useAdminTicketPriceCategories(
  options?: Partial<UseQueryOptions<AdminTicketPriceCategory[], Error, AdminTicketPriceCategory[]>>
): UseQueryResult<AdminTicketPriceCategory[], Error> {
  return useQuery({
    queryKey: adminTicketPriceCategoryQueryKeys.all,
    queryFn: ({ signal }) => getAdminTicketPriceCategories(undefined, signal),
    ...queryCacheStatic,
    ...options,
  });
}

export function createAdminTicketPriceCategory(
  body: UpsertTicketPriceCategoryBody
): Promise<AdminTicketPriceCategory> {
  return customInstance<AdminTicketPriceCategory>({
    url: '/api/admin/ticket-price-categories',
    method: 'POST',
    data: body,
  }).then((res) => unwrapData<AdminTicketPriceCategory>(res));
}

export function updateAdminTicketPriceCategory(
  id: string,
  body: UpsertTicketPriceCategoryBody
): Promise<AdminTicketPriceCategory> {
  return customInstance<AdminTicketPriceCategory>({
    url: `/api/admin/ticket-price-categories/${id}`,
    method: 'PUT',
    data: body,
  }).then((res) => unwrapData<AdminTicketPriceCategory>(res));
}
