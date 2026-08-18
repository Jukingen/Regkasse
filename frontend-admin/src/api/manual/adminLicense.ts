import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';
import { getEffectiveTenantSlug } from '@/features/auth/services/devTenant';
import { buildLicensesExportFileName } from '@/features/license/utils/licenseExportFileName';
import { triggerBrowserDownload } from '@/lib/zip/packFilesIntoZipBlob';

/** POS/FA mandant + deployment snapshot from <c>GET /api/license/status</c>. */
export type LicensePublicStatusDto = {
  /** Trial, Licensed, Expired, or Demo (development snapshot). */
  licenseType: 'Trial' | 'Licensed' | 'Expired' | 'Demo' | 'Paid' | string;
  /** ISO 8601 UTC from backend <c>DateTime?</c> JSON. */
  validUntil: string | null;
  daysRemaining: number;
  features: string[];
  isExpired: boolean;
  isValid: boolean;
  /** Demo (local dev bypass), Trial, or Production. */
  mode?: 'Demo' | 'Trial' | 'Production' | string;
  /** True when development-mode license bypass supplied the snapshot (Development host only). */
  isDevelopmentBypass?: boolean;
  /** Mandant access flag when <c>tenantId</c> query or tenant context is resolved. */
  canAccess?: boolean | null;
  /** Mandant transaction flag when tenant context is resolved. */
  canTransact?: boolean | null;
  /** German mandant status copy when tenant context is resolved (localized when Accept-Language is set). */
  statusMessage?: string | null;
  /** Stable message key for clients (`license.status.*`). */
  statusMessageKey?: string | null;
  /** True when mandant license is expired but still within the grace window. */
  isInGracePeriod?: boolean;
  /** True when mandant license is past grace (POS locked). */
  isLocked?: boolean;
  /** Elapsed whole days since expiry when expired; otherwise 0. */
  daysOverdue?: number;
  /** Remaining mandant grace days when <c>isInGracePeriod</c> is true. */
  gracePeriodRemaining?: number;
  /** ISO 8601 UTC when POS lock starts (expiry + grace); null when not expired. */
  lockDate?: string | null;
  /** Restriction codes (e.g. POS_LOCKED, SUPERADMIN_UNLOCK_ONLY). */
  restrictions?: string[];
  /** True when mandant license requires renewal (lockdown). */
  requiresRenewal?: boolean;
  /** Coarse combined label: active, grace, or expired. */
  status?: 'active' | 'grace' | 'expired' | string;
  systemLicense?: LicenseLayerPublicStatusDto | null;
  tenantLicense?: LicenseLayerPublicStatusDto | null;
  anyActive?: boolean;
  allActive?: boolean;
};

export type LicenseLayerPublicStatusDto = {
  validUntil?: string | null;
  status?: 'active' | 'grace' | 'expired' | 'locked' | string;
  isActive?: boolean;
};

/** Unified FA tenant license read-model cache key (POS contract: GET /api/license/status). */
export const tenantLicenseUnifiedQueryKey = ['tenant', 'license'] as const;

export type TenantLicenseQuerySource = 'admin' | 'public' | 'auto';

export function tenantLicenseUnifiedQueryKeyFor(
  tenantId?: string | null,
  source: TenantLicenseQuerySource = 'auto'
) {
  return [...tenantLicenseUnifiedQueryKey, tenantId ?? 'current', source] as const;
}

export type LicenseStatusResponse = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  /** ISO 8601 UTC timestamp; null when license has no exp claim (effectively unlimited paid). */
  expiryDate: string | null;
  machineHash: string;
  /** Enabled license feature ids; omitted on older servers (treat as full bundle). */
  enabledFeatures?: string[] | null;
  /** True when development-mode license bypass supplied the snapshot (Development host only). */
  isDevelopmentBypass?: boolean;
};

export type ActivateLicenseRequest = {
  licenseKey: string;
  offlineActivationJwt?: string | null;
  /** Super Admin platform activation target; ignored for system keys. */
  tenantId?: string | null;
};

export type LicenseActivationResult = {
  success: boolean;
  message?: string | null;
  /** ISO 8601 UTC when activation succeeded (backend). */
  validUntil?: string | null;
  /** Coarse label: Licensed, Trial, or Expired (aligned with GET /api/license/status). */
  licenseType?: string | null;
  tenantId?: string | null;
  tenantSlug?: string | null;
  apiBaseUrl?: string | null;
};

export type GenerateLicenseRequest = {
  customerName: string;
  /** Date-only (YYYY-MM-DD) interpreted as end-of-day UTC by the backend. */
  expiryDate: string;
  /** Sent as JSON bindToMachineFingerprint (backend merges with legacy requireFingerprint). */
  bindToMachineFingerprint: boolean;
  /** Required when bindToMachineFingerprint=true; lowercase hex SHA-256 (64 chars). */
  machineHashHex?: string | null;
  /** Optional explicit feature bundle; omitted = full single-license bundle. */
  features?: string[] | null;
};

export type GenerateLicenseResponse = {
  success: boolean;
  licenseKey: string | null;
  signedJwt: string | null;
  /** Alias returned by some API versions; same as signedJwt. */
  licenseJwt?: string | null;
  expiryAtUtc: string | null;
  message?: string | null;
};

export type IssuedLicenseListItemDto = {
  id: string;
  licenseKey: string;
  customerName: string;
  expiryAtUtc: string;
  requireFingerprint: boolean;
  machineHashHex: string | null;
  issuedAtUtc: string;
  issuedByUserId: string | null;
  isRevoked: boolean;
  revokedAtUtc: string | null;
  revocationReason: string | null;
  supersededByLicenseId?: string | null;
  transferredToLicenseId?: string | null;
  /** Distinct machines in `activated_licenses` for this license key. */
  activatedDeviceCount?: number;
  /** Latest activation timestamp (UTC) across devices. */
  lastActivationAtUtc?: string | null;
  /** Shortened SHA-256 hex (first 8 + last 8) for the device with the latest `last_seen`. */
  recentMachineFingerprintShort?: string | null;
  isCancelled?: boolean;
  isDeleted?: boolean;
  /** Enabled feature ids when stored on the issuance row. */
  features?: string[] | null;
};

export type IssuedLicensesListResponse = {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: IssuedLicenseListItemDto[];
};

export type IssuedLicensesListParams = {
  /** Customer name substring (optional). */
  search?: string | null;
  /** Substring match on activated machine fingerprint (SHA-256 hex, optional). */
  machineFingerprint?: string | null;
  pageNumber?: number;
  pageSize?: number;
};

export type LicenseActivationAttemptsListParams = {
  licenseKey?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  status?: string | null;
  machineFingerprint?: string | null;
  pageNumber?: number;
  pageSize?: number;
};

export type LicenseActivationAttemptListItemDto = {
  id: string;
  licenseKeyMasked: string;
  machineFingerprint: string;
  activationStatus: string;
  failureReason: string | null;
  clientIp: string | null;
  userAgent: string | null;
  activatedAtUtc: string;
  deactivatedAtUtc: string | null;
};

export type LicenseActivationAttemptsPagedResponse = {
  total: number;
  pageNumber: number;
  pageSize: number;
  items: LicenseActivationAttemptListItemDto[];
};

/** Query for <c>/api/admin/licenses/export/*</c> and <c>/report/summary</c>. */
export type LicenseExportReportParams = {
  /** ISO-8601 UTC; filters <c>issued_licenses.issued_at_utc</c> and activation rows when set. */
  fromUtc?: string | null;
  toUtc?: string | null;
  includeActivationHistory?: boolean;
  maskLicenseKeys?: boolean;
};

export type LicenseReportSummaryDto = {
  generatedAtUtc: string;
  issuedTotalInDateFilter: number;
  issuedActiveEligible: number;
  issuedRevoked: number;
  issuedCancelled: number;
  issuedDeleted: number;
  expiringWithin30Days: number;
  expiringWithin15Days: number;
  expiringWithin7Days: number;
  uniqueActivatedDevices: number;
  activationAttemptsInDateFilter: number;
};

export type LicenseAuditLogQueryParams = {
  page?: number;
  pageSize?: number;
  tenantId?: string;
  action?: string;
  fromUtc?: string;
  toUtc?: string;
  userSearch?: string;
};

export const licenseQueryKeys = {
  status: ['admin', 'license', 'status'] as const,
  deploymentStatus: ['admin', 'license', 'deploymentStatus'] as const,
  /** GET /api/license/status (anonymous, POS contract). */
  publicStatus: ['admin', 'license', 'publicStatus'] as const,
  listRoot: ['admin', 'license', 'list'] as const,
  list: (params: IssuedLicensesListParams) => [...licenseQueryKeys.listRoot, params] as const,
  activationAttemptsRoot: ['admin', 'license', 'activationAttempts'] as const,
  activationAttempts: (params: LicenseActivationAttemptsListParams) =>
    [...licenseQueryKeys.activationAttemptsRoot, params] as const,
  reportSummary: (params: LicenseExportReportParams) =>
    ['admin', 'licenses', 'report', 'summary', params] as const,
  reminderEmailPreview: (daysUntilExpiry: number) =>
    ['admin', 'license', 'reminder-email-preview', daysUntilExpiry] as const,
  auditLog: (params: LicenseAuditLogQueryParams) =>
    ['admin', 'license', 'audit', params] as const,
  renewalFunnel: (params?: { fromUtc?: string; toUtc?: string }) =>
    ['admin', 'license', 'renewal-funnel', params ?? {}] as const,
};

export async function getPublicLicenseStatus(): Promise<LicensePublicStatusDto> {
  return getTenantLicensePublicStatus();
}

/** GET /api/license/status — optional mandant overlay via <c>tenantId</c> (POS + FA unified contract). */
export async function getTenantLicensePublicStatus(
  tenantId?: string | null
): Promise<LicensePublicStatusDto> {
  const { data } = await AXIOS_INSTANCE.get<LicensePublicStatusDto>('/api/license/status', {
    params: tenantId ? { tenantId } : undefined,
  });
  return data;
}

export async function getLicenseStatus(): Promise<LicenseStatusResponse> {
  const { data } = await AXIOS_INSTANCE.get<LicenseStatusResponse>('/api/admin/license/status');
  return data;
}

export async function getDeploymentLicenseStatus(): Promise<LicenseStatusResponse> {
  const { data } = await AXIOS_INSTANCE.get<LicenseStatusResponse>(
    '/api/admin/license/deployment-status'
  );
  return data;
}

export async function postActivateLicense(
  body: ActivateLicenseRequest
): Promise<LicenseActivationResult> {
  const { data } = await AXIOS_INSTANCE.post<LicenseActivationResult>(
    '/api/license/activate',
    body,
    {
      headers: { 'X-App-Context': 'admin' },
    }
  );
  return data;
}

export async function getLicenseActivationAttempts(
  params: LicenseActivationAttemptsListParams | undefined
): Promise<LicenseActivationAttemptsPagedResponse> {
  const { data } = await AXIOS_INSTANCE.get<LicenseActivationAttemptsPagedResponse>(
    '/api/admin/license/activation-attempts',
    {
      params: {
        licenseKey: params?.licenseKey?.trim() ? params.licenseKey.trim() : undefined,
        fromUtc: params?.fromUtc?.trim() ? params.fromUtc.trim() : undefined,
        toUtc: params?.toUtc?.trim() ? params.toUtc.trim() : undefined,
        status: params?.status?.trim() ? params.status.trim() : undefined,
        machineFingerprint: params?.machineFingerprint?.trim()
          ? params.machineFingerprint.trim()
          : undefined,
        pageNumber: params?.pageNumber ?? 1,
        pageSize: params?.pageSize ?? 50,
      },
    }
  );
  return data;
}

export async function postForceDeactivateActivationAttempt(id: string): Promise<void> {
  await AXIOS_INSTANCE.post(
    `/api/admin/license/activation-attempts/${encodeURIComponent(id)}/force-deactivate`
  );
}

function licenseExportQueryParams(params: LicenseExportReportParams | undefined) {
  const p: Record<string, string | boolean | undefined> = {
    fromUtc: params?.fromUtc?.trim() ? params.fromUtc.trim() : undefined,
    toUtc: params?.toUtc?.trim() ? params.toUtc.trim() : undefined,
  };
  if (params && params.includeActivationHistory !== undefined) {
    p.includeActivationHistory = params.includeActivationHistory;
  }
  if (params && params.maskLicenseKeys !== undefined) {
    p.maskLicenseKeys = params.maskLicenseKeys;
  }
  return p;
}

export async function getLicenseReportSummary(
  params: LicenseExportReportParams | undefined
): Promise<LicenseReportSummaryDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseReportSummaryDto>(
    '/api/admin/licenses/report/summary',
    {
      params: licenseExportQueryParams(params),
    }
  );
  return data;
}

export type LicenseReminderEmailPreviewDto = {
  subject: string;
  htmlBody: string;
  plainBody: string;
  daysUntilExpiry: number;
  sampleExpiryDate: string;
};

export type LicenseReminderEmailPreviewParams = {
  daysUntilExpiry?: number;
  tenantName?: string;
  adminName?: string;
  expiryDate?: string;
};

/** GET /api/admin/license/reminder-email-preview — Super Admin sample HTML (no SMTP). */
export async function getLicenseReminderEmailPreview(
  params?: LicenseReminderEmailPreviewParams
): Promise<LicenseReminderEmailPreviewDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseReminderEmailPreviewDto>(
    '/api/admin/license/reminder-email-preview',
    {
      params: {
        daysUntilExpiry: params?.daysUntilExpiry ?? 7,
        tenantName: params?.tenantName,
        adminName: params?.adminName,
        expiryDate: params?.expiryDate,
      },
    }
  );
  return data;
}

export type LicenseAuditLogItem = {
  id: string;
  createdAtUtc: string;
  tenantId: string | null;
  tenantName: string | null;
  action: string;
  fromStatus: string | null;
  toStatus: string | null;
  performedBy: string | null;
  reason: string | null;
};

export type LicenseAuditLogListResponse = {
  items: LicenseAuditLogItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

/** GET /api/admin/license/audit — Super Admin unified license audit trail. */
export async function getLicenseAuditLog(
  params?: LicenseAuditLogQueryParams
): Promise<LicenseAuditLogListResponse> {
  const { data } = await AXIOS_INSTANCE.get<LicenseAuditLogListResponse>(
    '/api/admin/license/audit',
    {
      params: {
        page: params?.page ?? 1,
        pageSize: params?.pageSize ?? 20,
        tenantId: params?.tenantId,
        action: params?.action,
        fromUtc: params?.fromUtc,
        toUtc: params?.toUtc,
        userSearch: params?.userSearch,
      },
    }
  );
  return data;
}

/** GET /api/admin/license/audit/export — CSV of filtered audit rows. */
export async function downloadLicenseAuditLogCsv(
  params?: Omit<LicenseAuditLogQueryParams, 'page' | 'pageSize'>
): Promise<void> {
  const blob = await customInstance<Blob>({
    url: '/api/admin/license/audit/export',
    method: 'GET',
    responseType: 'blob',
    params: {
      tenantId: params?.tenantId,
      action: params?.action,
      fromUtc: params?.fromUtc,
      toUtc: params?.toUtc,
      userSearch: params?.userSearch,
    },
  });
  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '');
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `license-audit_${stamp}.csv`;
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}

/** GET /api/admin/license/certificate — mandant license PDF certificate. */
export async function downloadLicenseCertificatePdf(): Promise<void> {
  const blob = await customInstance<Blob>({
    url: '/api/admin/license/certificate',
    method: 'GET',
    responseType: 'blob',
  });
  const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, '');
  const url = globalThis.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `license-certificate_${stamp}.pdf`;
  anchor.click();
  globalThis.URL.revokeObjectURL(url);
}

export type LicenseRenewalFunnelDto = {
  total: number;
  reminderSent: number;
  pageViewed: number;
  renewed: number;
  activated: number;
  conversionRate: number;
  fromUtc: string;
  toUtc: string;
};

export type LicenseRenewalFunnelParams = {
  fromUtc?: string;
  toUtc?: string;
};

/** GET /api/admin/license/renewal-funnel — Super Admin conversion funnel. */
export async function getLicenseRenewalFunnel(
  params?: LicenseRenewalFunnelParams
): Promise<LicenseRenewalFunnelDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseRenewalFunnelDto>(
    '/api/admin/license/renewal-funnel',
    {
      params: {
        fromUtc: params?.fromUtc,
        toUtc: params?.toUtc,
      },
    }
  );
  return data;
}

/** POST /api/admin/license/renewal-funnel/page-view — record renewal UI view (deduped server-side). */
export async function postLicenseRenewalFunnelPageView(): Promise<void> {
  await AXIOS_INSTANCE.post('/api/admin/license/renewal-funnel/page-view');
}

export async function downloadLicenseExportFile(
  format: 'csv' | 'json',
  params: LicenseExportReportParams | undefined
): Promise<void> {
  const path =
    format === 'csv' ? '/api/admin/licenses/export/csv' : '/api/admin/licenses/export/json';
  const res = await AXIOS_INSTANCE.get<Blob>(path, {
    params: licenseExportQueryParams({
      ...params,
      includeActivationHistory: params?.includeActivationHistory,
      maskLicenseKeys: params?.maskLicenseKeys,
    }),
    responseType: 'blob',
  });
  const disposition = res.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)/i);
  const fileName =
    (match?.[1] ? decodeURIComponent(match[1].trim()) : null) ??
    buildLicensesExportFileName(getEffectiveTenantSlug(), format);
  triggerBrowserDownload(res.data, fileName);
}

export async function postGenerateLicense(
  body: GenerateLicenseRequest
): Promise<GenerateLicenseResponse> {
  const bind = body.bindToMachineFingerprint;
  const payload = {
    customerName: body.customerName,
    expiryDate: body.expiryDate,
    bindToMachineFingerprint: bind,
    /** Legacy/alternate binding flag — backend uses `bindToMachineFingerprint ?? requireFingerprint`. */
    requireFingerprint: bind,
    machineHashHex: body.machineHashHex?.trim()
      ? body.machineHashHex.trim().toLowerCase()
      : undefined,
  };
  const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
    '/api/admin/license/generate',
    payload
  );
  return data;
}

export async function getIssuedLicensesList(
  params: IssuedLicensesListParams | undefined
): Promise<IssuedLicensesListResponse> {
  const { data } = await AXIOS_INSTANCE.get<IssuedLicensesListResponse>('/api/admin/license/list', {
    params: {
      search: params?.search?.trim() ? params.search.trim() : undefined,
      machineFingerprint: params?.machineFingerprint?.trim()
        ? params.machineFingerprint.trim()
        : undefined,
      pageNumber: params?.pageNumber ?? 1,
      pageSize: params?.pageSize ?? 50,
    },
  });
  return data;
}

/** DELETE /api/admin/license/revoke/{id} */
export async function deleteRevokeIssuedLicense(id: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/license/revoke/${encodeURIComponent(id)}`);
}

/** POST /api/admin/license/renew — supply full `licenseKey` or list row `issuedLicenseId`, not both. */
export type RenewLicenseRequest = {
  licenseKey?: string;
  issuedLicenseId?: string;
  newExpiryDate: string;
};

export async function postRenewLicense(
  body: RenewLicenseRequest
): Promise<GenerateLicenseResponse> {
  const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
    '/api/admin/license/renew',
    body
  );
  return data;
}

/** POST /api/admin/license/upgrade — supply full `licenseKey` or list row `issuedLicenseId`, not both. */
export type UpgradeIssuedLicenseRequest = {
  licenseKey?: string;
  issuedLicenseId?: string;
  newExpiryDate: string;
  reason?: string | null;
};

export async function postUpgradeIssuedLicense(
  body: UpgradeIssuedLicenseRequest
): Promise<GenerateLicenseResponse> {
  const payload: Record<string, string> = { newExpiryDate: body.newExpiryDate };
  if (body.licenseKey?.trim()) {
    payload.licenseKey = body.licenseKey.trim();
  }
  if (body.issuedLicenseId) {
    payload.issuedLicenseId = body.issuedLicenseId;
  }
  if (body.reason?.trim()) {
    payload.reason = body.reason.trim();
  }
  const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
    '/api/admin/license/upgrade',
    payload
  );
  return data;
}

export type IssuedLicenseActivationDto = {
  machineFingerprint: string;
  activatedAtUtc: string;
  lastSeenAtUtc: string;
  validUntilUtc: string;
  customerName: string;
};

export type IssuedLicenseDetailResponse = {
  id: string;
  licenseKey: string;
  customerName: string;
  expiryAtUtc: string;
  requireFingerprint: boolean;
  machineHashHex: string | null;
  signedJwt: string;
  issuedAtUtc: string;
  issuedByUserId: string | null;
  isRevoked: boolean;
  revokedAtUtc: string | null;
  revocationReason: string | null;
  supersededByLicenseId: string | null;
  transferredToLicenseId: string | null;
  isCancelled: boolean;
  cancelledAtUtc: string | null;
  isDeleted: boolean;
  deletedAtUtc: string | null;
  activations: IssuedLicenseActivationDto[];
};

export type ExtendIssuedLicenseRequest = {
  addDays?: number;
  addMonths?: number;
};

export type RevokeIssuedLicenseByIdRequest = {
  reason?: string | null;
};

export type CancelIssuedLicenseRequest = {
  reason?: string | null;
};

export async function getIssuedLicenseDetail(
  issuedLicenseId: string
): Promise<IssuedLicenseDetailResponse> {
  const { data } = await AXIOS_INSTANCE.get<IssuedLicenseDetailResponse>(
    `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/details`
  );
  return data;
}

export async function postExtendIssuedLicense(
  issuedLicenseId: string,
  body: ExtendIssuedLicenseRequest
): Promise<GenerateLicenseResponse> {
  const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
    `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/extend`,
    body
  );
  return data;
}

export async function postRevokeIssuedLicenseById(
  issuedLicenseId: string,
  body?: RevokeIssuedLicenseByIdRequest
): Promise<void> {
  await AXIOS_INSTANCE.post(
    `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/revoke`,
    body ?? {}
  );
}

export async function postCancelIssuedLicense(
  issuedLicenseId: string,
  body?: CancelIssuedLicenseRequest
): Promise<void> {
  await AXIOS_INSTANCE.post(
    `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/cancel`,
    body ?? {}
  );
}

export async function deleteIssuedLicenseSoft(issuedLicenseId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/license/${encodeURIComponent(issuedLicenseId)}`);
}

export async function postUnregisterIssuedLicenseMachine(
  issuedLicenseId: string
): Promise<GenerateLicenseResponse> {
  const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
    `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/unregister-machine`
  );
  return data;
}

export type LicenseDashboardSummaryDto = {
  activeLicenses: number;
  expiringWithin30Days: number;
  expiredLicenses: number;
  uniqueActivatedDevices: number;
};

export type LicenseDashboardStatsDto = {
  totalTenants: number;
  activeTenantLicenses: number;
  expiringTenantLicenses: number;
  expiredTenantLicenses: number;
  graceTenantLicenses: number;
  lockedTenantLicenses: number;
  activeDeploymentLicenses: number;
  expiringDeploymentLicenses: number;
  expiredDeploymentLicenses: number;
  activatedDevices: number;
  recentActivities: LicenseActivityDto[];
};

export type LicenseActivityDto = {
  timestamp: string;
  licenseKey: string;
  machineHash: string;
  action: string;
  userEmail: string;
};

export type LicenseActivationSeriesPointDto = {
  periodStartUtc: string;
  count: number;
};

export type LicenseDashboardActivationSeriesDto = {
  granularity: string;
  points: LicenseActivationSeriesPointDto[];
};

export type LicenseDashboardActivityRowDto = {
  timestampUtc: string;
  licenseKeyMasked: string;
  machineFingerprintShort: string | null;
  action: string;
  sourceCode: string;
};

export type LicenseDashboardRecentActivityDto = {
  items: LicenseDashboardActivityRowDto[];
};

export const licenseDashboardQueryKeys = {
  summary: ['admin', 'license', 'dashboard', 'summary'] as const,
  stats: ['admin', 'license', 'dashboard', 'stats'] as const,
  series: (granularity: string, lookbackDays: number) =>
    ['admin', 'license', 'dashboard', 'series', granularity, lookbackDays] as const,
  activity: (take: number) => ['admin', 'license', 'dashboard', 'activity', take] as const,
};

export async function getLicenseDashboardSummary(): Promise<LicenseDashboardSummaryDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseDashboardSummaryDto>(
    '/api/admin/license/dashboard/summary'
  );
  return data;
}

export async function getLicenseDashboardStats(): Promise<LicenseDashboardStatsDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseDashboardStatsDto>(
    '/api/admin/license/dashboard-stats'
  );
  return data;
}

export async function getLicenseDashboardActivationSeries(params: {
  granularity: 'day' | 'week';
  lookbackDays?: number;
}): Promise<LicenseDashboardActivationSeriesDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseDashboardActivationSeriesDto>(
    '/api/admin/license/dashboard/activation-series',
    {
      params: {
        granularity: params.granularity,
        lookbackDays: params.lookbackDays ?? 30,
      },
    }
  );
  return data;
}

export async function getLicenseDashboardRecentActivity(
  take?: number
): Promise<LicenseDashboardRecentActivityDto> {
  const { data } = await AXIOS_INSTANCE.get<LicenseDashboardRecentActivityDto>(
    '/api/admin/license/dashboard/recent-activity',
    {
      params: { take: take ?? 25 },
    }
  );
  return data;
}

/** Triggers CSV download in the browser (UTF-8 BOM from server). */
export async function downloadLicenseDashboardReportCsv(): Promise<void> {
  const res = await AXIOS_INSTANCE.get<Blob>('/api/admin/license/dashboard/report.csv', {
    responseType: 'blob',
  });
  const blob = res.data;
  const disposition = res.headers['content-disposition'] as string | undefined;
  let fileName = 'license-dashboard-report.csv';
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)/i);
  if (match?.[1]) {
    fileName = decodeURIComponent(match[1].trim());
  }
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.rel = 'noopener';
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
