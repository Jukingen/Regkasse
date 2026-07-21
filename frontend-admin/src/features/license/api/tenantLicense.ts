import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantLicenseStatus = {
  kind: string;
  licenseKey?: string | null;
  validUntilUtc?: string | null;
  daysRemaining?: number | null;
  tier?: string | null;
  features: string[];
};

export type TenantLicenseHistoryItem = {
  issuedLicenseId?: string | null;
  eventType: string;
  atUtc: string;
  summary: string;
  licenseKey?: string | null;
  actorUserId?: string | null;
  actorDisplayName?: string | null;
};

export type TenantLicenseOverview = {
  status: TenantLicenseStatus;
  history: TenantLicenseHistoryItem[];
};

export type UpdateTenantLicenseRequest = {
  licenseKey?: string | null;
  validUntilUtc: string;
};

export type ExtendTenantLicenseResult = {
  success: boolean;
  licenseKey: string;
  validUntilUtc: string;
  status: string;
  message: string;
};

export type TenantLicensePreviewResult = {
  valid: boolean;
  licenseKey?: string | null;
  validFromUtc?: string | null;
  validUntilUtc?: string | null;
  durationDays?: number | null;
  durationDisplay?: string | null;
  status?: string | null;
  planName?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
};

export type PreviewTenantLicenseRequest = {
  licenseKey: string;
};

export type ExtendTenantLicenseRequest = {
  licenseKey: string;
};

export const tenantLicenseQueryKeys = {
  root: ['admin', 'tenant-license'] as const,
  detail: (tenantId: string) => [...tenantLicenseQueryKeys.root, tenantId] as const,
};

/** GET /api/admin/tenants/{tenantId}/license */
export async function getTenantLicense(tenantId: string): Promise<TenantLicenseOverview> {
  const { data } = await AXIOS_INSTANCE.get<TenantLicenseOverview>(
    `/api/admin/tenants/${tenantId}/license`
  );
  return data;
}

/** PUT /api/admin/tenants/{tenantId}/license */
export async function putTenantLicense(
  tenantId: string,
  body: UpdateTenantLicenseRequest
): Promise<TenantLicenseOverview> {
  const { data } = await AXIOS_INSTANCE.put<TenantLicenseOverview>(
    `/api/admin/tenants/${tenantId}/license`,
    body
  );
  return data;
}

/** POST /api/admin/license/mandant/preview — validate key without applying (Manager). */
export async function previewTenantLicense(
  body: PreviewTenantLicenseRequest
): Promise<TenantLicensePreviewResult> {
  const { data } = await AXIOS_INSTANCE.post<TenantLicensePreviewResult>(
    '/api/admin/license/mandant/preview',
    body
  );
  return data;
}

/** POST /api/admin/license/mandant/extend — extend effective tenant with REGK key (Manager).
 *  @deprecated Prefer `POST /api/admin/license/extend` (billing, `settings.manage`) — see `docs/BILLING_TENANT_LICENSE.md`. */
export async function extendTenantLicense(
  body: ExtendTenantLicenseRequest
): Promise<ExtendTenantLicenseResult> {
  const { data } = await AXIOS_INSTANCE.post<ExtendTenantLicenseResult>(
    '/api/admin/license/mandant/extend',
    body
  );
  return data;
}
