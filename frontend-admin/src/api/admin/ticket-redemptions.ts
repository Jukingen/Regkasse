import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export type TicketRedemptionStatus = 'Valid' | 'Redeemed' | 'Cancelled' | 'Expired';

export interface AdminTicketRedemptionRow {
  id: string;
  tenantId: string;
  displayCode: string;
  status: string;
  validUntilUtc?: string | null;
  redeemedAtUtc?: string | null;
  redeemedByUserId?: string | null;
}

export interface AdminTicketRedemptionListResponse {
  items: AdminTicketRedemptionRow[];
}

export type AdminTicketRedemptionListParams = {
  status?: string;
  tenantId?: string;
};

function redemptionsUrl(params?: AdminTicketRedemptionListParams): string {
  const search = new URLSearchParams();
  if (params?.status) search.set('status', params.status);
  if (params?.tenantId) search.set('tenantId', params.tenantId);
  const query = search.toString();
  return query ? `/api/admin/tickets/redemptions?${query}` : '/api/admin/tickets/redemptions';
}

export const adminTicketRedemptionQueryKeys = {
  all: ['admin', 'ticket-redemptions'] as const,
  list: (params?: AdminTicketRedemptionListParams) =>
    [...adminTicketRedemptionQueryKeys.all, params ?? {}] as const,
};

export function getAdminTicketRedemptions(
  params?: AdminTicketRedemptionListParams,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminTicketRedemptionListResponse> {
  return customInstance<AdminTicketRedemptionListResponse>(
    { url: redemptionsUrl(params), method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminTicketRedemptionListResponse>(res));
}

export function useAdminTicketRedemptions(
  params?: AdminTicketRedemptionListParams,
  options?: Partial<
    UseQueryOptions<AdminTicketRedemptionListResponse, Error, AdminTicketRedemptionListResponse>
  >
): UseQueryResult<AdminTicketRedemptionListResponse, Error> {
  return useQuery({
    queryKey: adminTicketRedemptionQueryKeys.list(params),
    queryFn: ({ signal }) => getAdminTicketRedemptions(params, undefined, signal),
    ...options,
  });
}
