import { useQuery, type UseQueryOptions, type UseQueryResult } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export const HAIR_SALON_CAMPAIGN_EXTENSION = 'hair_salon_campaign';

export interface HairSalonCampaign {
  id: string;
  name: string;
  discountPercent: number;
  appliesToProductIds: string[];
  validFromUtc: string;
  validUntilUtc: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertHairSalonCampaignRequest {
  name: string;
  discountPercent: number;
  appliesToProductIds: string[];
  validFromUtc: string;
  validUntilUtc: string;
  isActive?: boolean;
}

export const hairSalonCampaignQueryKeys = {
  all: ['admin', 'hair-salon-campaigns'] as const,
  list: () => [...hairSalonCampaignQueryKeys.all, 'list'] as const,
};

export function listHairSalonCampaigns(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<HairSalonCampaign[]> {
  return customInstance<HairSalonCampaign[]>(
    { url: '/api/admin/extensions/hair-salon-campaigns', method: 'GET', signal },
    options
  ).then((res) => unwrapData<HairSalonCampaign[]>(res));
}

export function createHairSalonCampaign(
  body: UpsertHairSalonCampaignRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<HairSalonCampaign> {
  return customInstance<HairSalonCampaign>(
    { url: '/api/admin/extensions/hair-salon-campaigns', method: 'POST', data: body },
    options
  ).then((res) => unwrapData<HairSalonCampaign>(res));
}

export function updateHairSalonCampaign(
  id: string,
  body: UpsertHairSalonCampaignRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<HairSalonCampaign> {
  return customInstance<HairSalonCampaign>(
    {
      url: `/api/admin/extensions/hair-salon-campaigns/${encodeURIComponent(id)}`,
      method: 'PUT',
      data: body,
    },
    options
  ).then((res) => unwrapData<HairSalonCampaign>(res));
}

export function deactivateHairSalonCampaign(
  id: string,
  options?: SecondParameter<typeof customInstance>
): Promise<void> {
  return customInstance<void>(
    {
      url: `/api/admin/extensions/hair-salon-campaigns/${encodeURIComponent(id)}`,
      method: 'DELETE',
    },
    options
  ).then(() => undefined);
}

export function useHairSalonCampaigns(
  options?: Partial<UseQueryOptions<HairSalonCampaign[], Error, HairSalonCampaign[]>>
): UseQueryResult<HairSalonCampaign[], Error> {
  return useQuery({
    queryKey: hairSalonCampaignQueryKeys.list(),
    queryFn: ({ signal }) => listHairSalonCampaigns(undefined, signal),
    ...options,
  });
}
