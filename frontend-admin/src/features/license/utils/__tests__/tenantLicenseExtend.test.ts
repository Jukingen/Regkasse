import { describe, expect, it } from 'vitest';

import {
  displayLicenseKey,
  maskTenantLicenseKey,
} from '@/features/license/utils/tenantLicenseExtend';

describe('maskTenantLicenseKey', () => {
  it('masks long REGK keys', () => {
    expect(maskTenantLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe('REGK-AAA…CCCC');
  });

  it('returns dash for empty', () => {
    expect(maskTenantLicenseKey(null)).toBe('—');
  });
});

describe('displayLicenseKey', () => {
  it('returns the full key when reveal is true', () => {
    expect(displayLicenseKey('REGK-20990101-system-ABCDEF12', true)).toBe(
      'REGK-20990101-system-ABCDEF12'
    );
  });

  it('masks when reveal is false', () => {
    expect(displayLicenseKey('REGK-20990101-system-ABCDEF12', false)).toBe(
      'REGK-209…EF12'
    );
  });
});
