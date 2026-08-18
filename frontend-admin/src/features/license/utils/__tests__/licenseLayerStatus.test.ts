import { describe, expect, it } from 'vitest';

import {
  isLicenseLayerActive,
  isSystemActiveTenantLocked,
  resolveLicenseLayerLabelKey,
} from '../licenseLayerStatus';

describe('licenseLayerStatus', () => {
  it('treats active and grace layers as operational', () => {
    expect(isLicenseLayerActive({ status: 'active', isActive: true })).toBe(true);
    expect(isLicenseLayerActive({ status: 'grace', isActive: true })).toBe(true);
    expect(isLicenseLayerActive({ status: 'locked', isActive: false })).toBe(false);
    expect(isLicenseLayerActive({ status: 'expired', isActive: false })).toBe(false);
  });

  it('detects system-active tenant-locked', () => {
    expect(
      isSystemActiveTenantLocked({
        systemLicense: { status: 'active', isActive: true, validUntil: '2026-08-19T00:00:00Z' },
        tenantLicense: { status: 'locked', isActive: false, validUntil: '2026-07-01T00:00:00Z' },
      })
    ).toBe(true);
    expect(
      isSystemActiveTenantLocked({
        systemLicense: { status: 'active', isActive: true },
        tenantLicense: { status: 'active', isActive: true },
      })
    ).toBe(false);
  });

  it('maps layer labels', () => {
    expect(resolveLicenseLayerLabelKey({ status: 'active', isActive: true })).toBe('active');
    expect(resolveLicenseLayerLabelKey({ status: 'grace', isActive: true })).toBe('grace');
    expect(resolveLicenseLayerLabelKey({ status: 'locked', isActive: false })).toBe('locked');
    expect(resolveLicenseLayerLabelKey({ status: 'expired', isActive: false })).toBe('expired');
  });
});
