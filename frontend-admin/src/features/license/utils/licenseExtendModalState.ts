import type {
  ExtendTenantLicenseResult,
  TenantLicensePreviewResult,
} from '@/features/license/api/tenantLicense';

export type LicenseExtendUiState =
  'empty' | 'loading' | 'valid' | 'invalid' | 'confirming' | 'success';

export function resolveLicenseExtendUiState(params: {
  preview: TenantLicensePreviewResult | null;
  extendResult: ExtendTenantLicenseResult | null;
  isPreviewLoading: boolean;
  isExtendPending: boolean;
}): LicenseExtendUiState {
  if (params.extendResult) return 'success';
  if (params.isExtendPending) return 'confirming';
  if (params.isPreviewLoading) return 'loading';
  if (params.preview?.valid) return 'valid';
  if (params.preview && !params.preview.valid) return 'invalid';
  return 'empty';
}

export function isLicenseExtendPreviewVisible(state: LicenseExtendUiState): boolean {
  return state !== 'empty';
}

export function isLicenseKeyInputDisabled(state: LicenseExtendUiState): boolean {
  return state === 'loading' || state === 'confirming' || state === 'success';
}

export function isLicensePreviewButtonDisabled(state: LicenseExtendUiState): boolean {
  return state === 'loading' || state === 'confirming' || state === 'success';
}

export function isLicenseExtendConfirmEnabled(state: LicenseExtendUiState): boolean {
  return state === 'valid';
}
