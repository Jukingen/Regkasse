import { AXIOS_INSTANCE } from '@/lib/axios';

export type TrialTenantSummary = {
  tenantId: string;
  name: string;
  slug: string;
  email?: string | null;
  trialStatus?: string | null;
  trialStartedAtUtc?: string | null;
  trialEndsAtUtc?: string | null;
  trialGracePeriodEndsAtUtc?: string | null;
  trialConvertedAtUtc?: string | null;
  trialDeletedAtUtc?: string | null;
  daysRemaining?: number | null;
  reminder7dSent: boolean;
  reminder3dSent: boolean;
  reminder1dSent: boolean;
};

export type TrialDashboard = {
  activeCount: number;
  expiringSoonCount: number;
  expiredCount: number;
  convertedCount: number;
  deletedCount: number;
  conversionRatePercent: number;
  activeTrials: TrialTenantSummary[];
  expiringSoon: TrialTenantSummary[];
  expiredTrials: TrialTenantSummary[];
};

export type TrialAnalytics = {
  trialsCreatedLast30Days: number;
  activeTrials: number;
  expiredTrials: number;
  convertedTrials: number;
  deletedTrials: number;
  conversionRatePercent: number;
  averageDaysToConvert?: number | null;
  mostCommonLicensePlan?: string | null;
  conversionByTrialDuration: Array<{
    trialDurationDays: number;
    convertedCount: number;
    totalStarted: number;
  }>;
  conversionByPlan?: Array<{
    licensePlan: string;
    convertedCount: number;
  }>;
  monthlyTrend?: Array<{
    yearMonth: string;
    trialsStarted: number;
    converted: number;
  }>;
};

export type TrialConversionResult = {
  success: boolean;
  tenantId: string;
  licenseSaleId: string;
  licenseValidUntilUtc: string;
  conversionDateUtc: string;
  remainingTrialDaysAdded: number;
  licensePlan?: string | null;
  licenseKey?: string | null;
  error?: string | null;
};

export async function fetchTrialDashboard(): Promise<TrialDashboard> {
  const { data } = await AXIOS_INSTANCE.get<TrialDashboard>('/api/admin/trials');
  return data;
}

export async function fetchTrialAnalytics(): Promise<TrialAnalytics> {
  const { data } = await AXIOS_INSTANCE.get<TrialAnalytics>('/api/admin/trials/analytics');
  return data;
}

export async function extendTrial(
  tenantId: string,
  additionalDays: number
): Promise<TrialTenantSummary> {
  const { data } = await AXIOS_INSTANCE.post<TrialTenantSummary>(
    `/api/admin/trials/tenants/${tenantId}/extend`,
    { additionalDays }
  );
  return data;
}

export async function convertTrialToPaid(
  tenantId: string,
  licenseSaleId: string,
  options?: { addRemainingTrialDays?: boolean; notes?: string }
): Promise<TrialConversionResult> {
  const { data } = await AXIOS_INSTANCE.post<TrialConversionResult>(
    `/api/admin/trials/tenants/${tenantId}/convert-to-paid`,
    {
      licenseSaleId,
      addRemainingTrialDays: options?.addRemainingTrialDays ?? true,
      notes: options?.notes,
    }
  );
  return data;
}

/** Manager-accessible conversion on the tenant license route. */
export async function convertTenantLicenseToPaid(
  tenantId: string,
  licenseSaleId: string,
  options?: { addRemainingTrialDays?: boolean; notes?: string }
): Promise<TrialConversionResult> {
  const { data } = await AXIOS_INSTANCE.post<TrialConversionResult>(
    `/api/admin/tenants/${tenantId}/license/convert-to-paid`,
    {
      licenseSaleId,
      addRemainingTrialDays: options?.addRemainingTrialDays ?? true,
      notes: options?.notes,
    }
  );
  return data;
}

export async function softDeleteTrial(tenantId: string): Promise<void> {
  await AXIOS_INSTANCE.post(`/api/admin/trials/tenants/${tenantId}/delete`);
}
