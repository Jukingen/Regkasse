import type { TenantLimitUsageDto } from '@/features/tenants/api/tenantLimits';
import { AXIOS_INSTANCE } from '@/lib/axios';

import type { DevLimitKey, DevLimitScenario } from '../constants/limitKeys';

export const devLimitsQueryKey = (tenantId?: string | null) =>
  ['dev', 'limits', tenantId ?? 'none'] as const;

export type SetDevLimitPayload = {
  tenantId: string;
  limitKey: DevLimitKey;
  value: number;
};

export type TriggerDevLimitScenarioPayload = {
  tenantId: string;
  scenario: DevLimitScenario;
  limitKey?: DevLimitKey;
};

export async function getDevLimitStatus(tenantId: string): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.get<TenantLimitUsageDto>('/api/dev/limits/status', {
    params: { tenantId },
  });
  return data;
}

export async function setDevLimit(payload: SetDevLimitPayload): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.post<TenantLimitUsageDto>('/api/dev/limits/set', payload);
  return data;
}

export async function resetAllDevLimits(tenantId: string): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.post<TenantLimitUsageDto>('/api/dev/limits/reset-all', null, {
    params: { tenantId },
  });
  return data;
}

export async function triggerDevLimitScenario(
  payload: TriggerDevLimitScenarioPayload
): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.post<TenantLimitUsageDto>(
    '/api/dev/limits/scenario/trigger',
    payload
  );
  return data;
}

export async function clearDevLimitCache(tenantId: string): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.post<TenantLimitUsageDto>('/api/dev/limits/cache/clear', null, {
    params: { tenantId },
  });
  return data;
}
