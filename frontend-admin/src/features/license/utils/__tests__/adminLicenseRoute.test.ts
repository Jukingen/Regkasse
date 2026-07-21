import { describe, expect, it } from 'vitest';

import {
  ADMIN_LICENSE_PAGE_INTENT_EXTEND,
  ADMIN_LICENSE_PAGE_PATH,
  buildAdminLicensePageHref,
  readAdminLicensePagePrefill,
} from '@/features/license/utils/adminLicenseRoute';
import { isPathAllowedWithoutTenant } from '@/features/tenancy/hooks/useSuperAdminTenantMode';

describe('adminLicenseRoute', () => {
  it('builds extend href with machine hash', () => {
    expect(
      buildAdminLicensePageHref({
        machineHash: 'abcdef',
        intent: ADMIN_LICENSE_PAGE_INTENT_EXTEND,
      })
    ).toBe('/admin/license?intent=extend&machineHash=abcdef');
  });

  it('reads valid machine hash prefill and enables fingerprint binding', () => {
    const params = new globalThis.URLSearchParams({
      intent: 'extend',
      machineHash: 'A'.repeat(64),
    });

    expect(readAdminLicensePagePrefill(params)).toEqual({
      intent: 'extend',
      machineHashHex: 'a'.repeat(64),
      requireFingerprint: true,
    });
  });

  it('ignores invalid machine hash values', () => {
    const params = new globalThis.URLSearchParams({
      intent: 'extend',
      machineHash: 'not-a-hash',
    });

    expect(readAdminLicensePagePrefill(params)).toEqual({
      intent: 'extend',
      machineHashHex: undefined,
      requireFingerprint: false,
    });
  });

  it('keeps the license page reachable without tenant context', () => {
    expect(ADMIN_LICENSE_PAGE_PATH).toBe('/admin/license');
    expect(isPathAllowedWithoutTenant(ADMIN_LICENSE_PAGE_PATH)).toBe(true);
  });
});
