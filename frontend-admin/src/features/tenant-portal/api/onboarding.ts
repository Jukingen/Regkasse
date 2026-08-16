import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantOnboardingOverview = {
  tenantId: string;
  completedCount: number;
  totalCount: number;
  isFullyComplete: boolean;
  steps: { step: string; isCompleted: boolean; completedAtUtc?: string | null }[];
};

export const tenantOnboardingQueryKeys = {
  all: ['tenant-portal', 'onboarding'] as const,
  byTenant: (tenantId: string) => [...tenantOnboardingQueryKeys.all, tenantId] as const,
};

export async function fetchTenantOnboarding(
  tenantId: string,
  signal?: AbortSignal
): Promise<TenantOnboardingOverview> {
  const { data } = await AXIOS_INSTANCE.get<TenantOnboardingOverview>(
    `/api/admin/onboarding/${tenantId}`,
    { signal }
  );
  return data;
}
