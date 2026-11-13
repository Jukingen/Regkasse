/// <reference types="jest" />

import {
  isValidPosLicenseKey,
  sanitizeLicenseKeyInput,
} from '../utils/licenseKeyFormat';

describe('licenseKeyFormat', () => {
  it('accepts legacy display and unified system keys', () => {
    expect(isValidPosLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe(true);
    expect(isValidPosLicenseKey('REGK-20260813-system-1R61EMER')).toBe(true);
    expect(isValidPosLicenseKey('REGK-20261231-dev-A7F3K2D9')).toBe(true);
    expect(isValidPosLicenseKey('not-a-key')).toBe(false);
  });

  it('does not truncate unified keys during sanitize', () => {
    const typed = 'regk-20260813-system-1r61emer';
    expect(sanitizeLicenseKeyInput(typed)).toBe('REGK-20260813-SYSTEM-1R61EMER');
    expect(sanitizeLicenseKeyInput(typed).length).toBe(typed.length);
  });
});
