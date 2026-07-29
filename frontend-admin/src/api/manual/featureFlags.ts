/**
 * Super Admin feature-flag API (manual; not Orval).
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

export type FeatureFlagStatusDto = {
  name: string;
  enabled: boolean;
  configDefault: boolean;
  overrideValue?: boolean | null;
  source: 'config' | 'global_override' | 'tenant_override' | string;
  tenantId?: string | null;
};

export type SetFeatureFlagRequest = {
  name: string;
  enabled: boolean;
  tenantId?: string | null;
  clearOverride?: boolean;
};

export async function fetchFeatureFlags(
  tenantId?: string | null,
  signal?: AbortSignal,
): Promise<FeatureFlagStatusDto[]> {
  const { data } = await AXIOS_INSTANCE.get<FeatureFlagStatusDto[]>('/api/admin/feature-flags', {
    params: tenantId ? { tenantId } : undefined,
    signal,
  });
  return data ?? [];
}

export async function setFeatureFlag(
  body: SetFeatureFlagRequest,
): Promise<FeatureFlagStatusDto> {
  const { data } = await AXIOS_INSTANCE.put<FeatureFlagStatusDto>('/api/admin/feature-flags', body);
  return data;
}
