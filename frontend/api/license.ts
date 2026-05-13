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

export const licenseApi = {
  /** GET /api/license/status (anonymous). */
  getStatus(): Promise<LicensePublicStatusDto> {
    return apiClient.get<LicensePublicStatusDto>('/license/status');
  },
};
