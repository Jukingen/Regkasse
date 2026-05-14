import { apiClient } from '../services/api/config';

/** Matches backend `LicensePublicStatusDto` (camelCase JSON). */
export type LicensePublicStatusDto = {
  licenseType: string;
  validUntil: string | null;
  daysRemaining: number;
  features: readonly string[];
  isExpired: boolean;
  isValid: boolean;
  mode?: string;
};

/** Matches backend `LicenseActivationResult` (camelCase JSON). */
export type LicenseActivationResultDto = {
  success: boolean;
  message?: string | null;
  validUntil?: string | null;
  /** Licensed, Trial, or Expired (same coarse labels as GET /api/license/status). */
  licenseType?: string | null;
};

const APP_CONTEXT_HEADER = 'X-App-Context';

export const licenseApi = {
  /** GET /api/license/status (anonymous). */
  getStatus(): Promise<LicensePublicStatusDto> {
    return apiClient.get<LicensePublicStatusDto>('/license/status');
  },

  /** POST /api/license/activate (anonymous; display key only). */
  activate(licenseKey: string): Promise<LicenseActivationResultDto> {
    return apiClient.post<LicenseActivationResultDto>(
      '/license/activate',
      { licenseKey },
      { headers: { [APP_CONTEXT_HEADER]: 'pos' } },
    );
  },
};
