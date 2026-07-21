import { describe, expect, it } from 'vitest';

import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';

describe('maskTenantLicenseKey', () => {
  it('masks long REGK keys', () => {
    expect(maskTenantLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe('REGK-AAA…CCCC');
  });

  it('returns dash for empty', () => {
    expect(maskTenantLicenseKey(null)).toBe('—');
  });
});
