import { AXIOS_INSTANCE } from '@/lib/axios';

export type CurrentTenantDto = {
  id: string;
  slug: string;
  name: string;
  licenseValid: boolean;
  licenseValidUntilUtc: string | null;
};

/** GET /api/tenants/current — server-resolved mandant for the active request. */
export async function getCurrentTenant(): Promise<CurrentTenantDto> {
  const { data } = await AXIOS_INSTANCE.get<CurrentTenantDto>('/api/tenants/current');
  return data;
}

export const currentTenantQueryKey = ['tenant', 'current'] as const;
