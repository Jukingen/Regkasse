import { apiClient, applyStoredApiBaseUrl } from '../services/api/config';
import { tenantStorage } from '../services/tenant/tenantStorage';

/** Matches backend `LicensePublicStatusDto` (camelCase JSON). */
export type LicensePublicStatusDto = {
  licenseType: string;
  validUntil: string | null;
  daysRemaining: number;
  features: readonly string[];
  isExpired: boolean;
  isValid: boolean;
  mode?: string;
  canAccess?: boolean | null;
  canTransact?: boolean | null;
  statusMessage?: string | null;
  statusMessageKey?: string | null;
  isInGracePeriod?: boolean;
  isLocked?: boolean;
  daysOverdue?: number;
  gracePeriodRemaining?: number;
  lockDate?: string | null;
  restrictions?: readonly string[];
  requiresRenewal?: boolean;
};

/** Mandant fields returned when GET /api/license/status includes tenant context. */
export type TenantLicenseStatusDto = Pick<
  LicensePublicStatusDto,
  | 'canAccess'
  | 'canTransact'
  | 'statusMessage'
  | 'statusMessageKey'
  | 'daysRemaining'
  | 'daysOverdue'
  | 'isInGracePeriod'
  | 'isLocked'
  | 'gracePeriodRemaining'
  | 'lockDate'
  | 'restrictions'
  | 'validUntil'
>;

/** Matches backend `LicenseActivationResult` (camelCase JSON). */
export type LicenseActivationResultDto = {
  success: boolean;
  message?: string | null;
  validUntil?: string | null;
  /** Licensed, Trial, or Expired (same coarse labels as GET /api/license/status). */
  licenseType?: string | null;
  tenantId?: string | null;
  tenantSlug?: string | null;
  apiBaseUrl?: string | null;
};

const APP_CONTEXT_HEADER = 'X-App-Context';

export const licenseApi = {
  /** GET /api/license/status (anonymous). */
  getStatus(): Promise<LicensePublicStatusDto> {
    return apiClient.get<LicensePublicStatusDto>('/license/status');
  },

  /** GET /api/license/status?tenantId=… (authenticated or dev tenant header). */
  getTenantLicenseStatus(tenantId: string): Promise<TenantLicenseStatusDto> {
    const query = encodeURIComponent(tenantId);
    return apiClient.get<TenantLicenseStatusDto>(`/license/status?tenantId=${query}`);
  },

  /** POST /api/license/activate (anonymous; display key only). */
  async activate(licenseKey: string): Promise<LicenseActivationResultDto> {
    const res = await apiClient.post<LicenseActivationResultDto>(
      '/license/activate',
      { licenseKey },
      { headers: { [APP_CONTEXT_HEADER]: 'pos' } }
    );

    if (res.success) {
      await tenantStorage.persistBootstrap({
        tenantId: res.tenantId,
        tenantSlug: res.tenantSlug,
        apiBaseUrl: res.apiBaseUrl,
      });
      if (res.apiBaseUrl) {
        applyStoredApiBaseUrl(res.apiBaseUrl);
      }
    }

    return res;
  },
};
