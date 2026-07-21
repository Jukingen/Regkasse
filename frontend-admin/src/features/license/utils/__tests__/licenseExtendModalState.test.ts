import { describe, expect, it } from 'vitest';

import {
  isLicenseExtendConfirmEnabled,
  isLicenseExtendPreviewVisible,
  isLicenseKeyInputDisabled,
  resolveLicenseExtendUiState,
} from '@/features/license/utils/licenseExtendModalState';

describe('resolveLicenseExtendUiState', () => {
  it('returns empty when no preview', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: null,
        extendResult: null,
        isPreviewLoading: false,
        isExtendPending: false,
      })
    ).toBe('empty');
  });

  it('returns loading while preview is fetching', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: null,
        extendResult: null,
        isPreviewLoading: true,
        isExtendPending: false,
      })
    ).toBe('loading');
  });

  it('returns valid for successful preview', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: { valid: true, status: 'valid' },
        extendResult: null,
        isPreviewLoading: false,
        isExtendPending: false,
      })
    ).toBe('valid');
  });

  it('returns invalid for failed preview', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: { valid: false, errorCode: 'invalid_key' },
        extendResult: null,
        isPreviewLoading: false,
        isExtendPending: false,
      })
    ).toBe('invalid');
  });

  it('returns confirming while extend is pending', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: { valid: true, status: 'valid' },
        extendResult: null,
        isPreviewLoading: false,
        isExtendPending: true,
      })
    ).toBe('confirming');
  });

  it('returns success after extend completes', () => {
    expect(
      resolveLicenseExtendUiState({
        preview: { valid: true, status: 'valid' },
        extendResult: {
          success: true,
          licenseKey: 'REGK-AAAAA-BBBBB-CCCCC',
          validUntilUtc: '2026-12-31T00:00:00Z',
          status: 'active',
          message: 'ok',
        },
        isPreviewLoading: false,
        isExtendPending: false,
      })
    ).toBe('success');
  });
});

describe('license extend ui guards', () => {
  it('hides preview area when empty', () => {
    expect(isLicenseExtendPreviewVisible('empty')).toBe(false);
    expect(isLicenseExtendPreviewVisible('valid')).toBe(true);
  });

  it('enables confirm only for valid preview', () => {
    expect(isLicenseExtendConfirmEnabled('valid')).toBe(true);
    expect(isLicenseExtendConfirmEnabled('invalid')).toBe(false);
    expect(isLicenseExtendConfirmEnabled('loading')).toBe(false);
  });

  it('disables input while loading, confirming, or success', () => {
    expect(isLicenseKeyInputDisabled('empty')).toBe(false);
    expect(isLicenseKeyInputDisabled('loading')).toBe(true);
    expect(isLicenseKeyInputDisabled('confirming')).toBe(true);
    expect(isLicenseKeyInputDisabled('success')).toBe(true);
  });
});
