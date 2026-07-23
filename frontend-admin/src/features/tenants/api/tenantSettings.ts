import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantSettingType = 'currency' | 'country' | 'timezone' | 'fiscal_settings';

export type TenantSettingStatus = 'pending' | 'approved' | 'rejected' | 'reverted';

export type FiscalSettingsValue = {
  companyName: string;
  companyAddress: string;
  companyTaxNumber: string;
  companyVatNumber?: string | null;
  companyRegistrationNumber?: string | null;
};

export type CurrentTenantSettings = {
  tenantId: string;
  currency: string;
  country: string;
  timeZone: string;
  fiscalSettings: FiscalSettingsValue;
  /** TSE-signed payments exist (RKSV fiscal footprint). */
  hasFiscalData?: boolean;
  /** Any invoice rows exist for this tenant. */
  hasInvoices?: boolean;
};

export type TenantSettingsHistoryItem = {
  id: string;
  tenantId: string;
  settingType: TenantSettingType | string;
  oldValue: string | null;
  newValue: string | null;
  status: TenantSettingStatus | string;
  requestedBy: string;
  approvedBy: string | null;
  requestedAt: string;
  approvedAt: string | null;
  effectiveAt: string | null;
  reason: string | null;
  notes: string | null;
  createdAt: string;
};

export type RequestTenantSettingsChangeBody = {
  tenantId: string;
  settingType: TenantSettingType;
  newValue: string | FiscalSettingsValue;
  reason: string;
};

export type SettingsChangeResult = {
  succeeded: boolean;
  changeId?: string | null;
  error?: string | null;
  errorCode?: string | null;
  warning?: string | null;
};

export const tenantSettingsQueryKeys = {
  root: ['admin', 'tenant-settings'] as const,
  current: (tenantId: string) => [...tenantSettingsQueryKeys.root, 'current', tenantId] as const,
  history: (tenantId: string) => [...tenantSettingsQueryKeys.root, 'history', tenantId] as const,
};

/** GET /api/admin/tenant-settings/current?tenantId= */
export async function getTenantSettings(tenantId: string): Promise<CurrentTenantSettings> {
  const { data } = await AXIOS_INSTANCE.get<CurrentTenantSettings>(
    '/api/admin/tenant-settings/current',
    { params: { tenantId } }
  );
  return data;
}

/** GET /api/admin/tenant-settings/history?tenantId= */
export async function getSettingsHistory(
  tenantId: string,
  params?: { fromDate?: string; toDate?: string; status?: string }
): Promise<TenantSettingsHistoryItem[]> {
  const { data } = await AXIOS_INSTANCE.get<TenantSettingsHistoryItem[]>(
    '/api/admin/tenant-settings/history',
    { params: { tenantId, ...params } }
  );
  return data;
}

/** POST /api/admin/tenant-settings/request */
export async function requestSettingsChange(
  body: RequestTenantSettingsChangeBody
): Promise<SettingsChangeResult> {
  const { data } = await AXIOS_INSTANCE.post<SettingsChangeResult>(
    '/api/admin/tenant-settings/request',
    body
  );
  return data;
}

/** POST /api/admin/tenant-settings/{changeId}/approve */
export async function approveSettingsChange(changeId: string): Promise<SettingsChangeResult> {
  const { data } = await AXIOS_INSTANCE.post<SettingsChangeResult>(
    `/api/admin/tenant-settings/${changeId}/approve`
  );
  return data;
}

/** POST /api/admin/tenant-settings/{changeId}/reject */
export async function rejectSettingsChange(
  changeId: string,
  reason: string
): Promise<SettingsChangeResult> {
  const { data } = await AXIOS_INSTANCE.post<SettingsChangeResult>(
    `/api/admin/tenant-settings/${changeId}/reject`,
    { reason }
  );
  return data;
}

/** POST /api/admin/tenant-settings/{changeId}/revert */
export async function revertSettingsChange(
  changeId: string,
  reason: string
): Promise<SettingsChangeResult> {
  const { data } = await AXIOS_INSTANCE.post<SettingsChangeResult>(
    `/api/admin/tenant-settings/${changeId}/revert`,
    { reason }
  );
  return data;
}

export function formatSettingsJsonValue(value: string | null | undefined): string {
  if (value == null || value === '') return '—';
  try {
    const parsed: unknown = JSON.parse(value);
    if (typeof parsed === 'string') return parsed;
    return JSON.stringify(parsed, null, 0);
  } catch {
    return value;
  }
}
