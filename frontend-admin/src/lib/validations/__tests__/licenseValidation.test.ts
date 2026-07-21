import { describe, expect, it } from 'vitest';

import {
  createLicenseKeyFormRules,
  detectLicenseKeyKind,
  isValidBillingLicenseKey,
  isValidDisplayLicenseKey,
  isValidLicenseKey,
  normalizeLicenseKeyInput,
} from '@/lib/validations/licenseValidation';

describe('licenseValidation', () => {
  it('accepts display format REGK-XXXXX-XXXXX-XXXXX', () => {
    expect(isValidDisplayLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe(true);
    expect(isValidDisplayLicenseKey('regk-abcde-12345-fghij')).toBe(true);
    expect(isValidDisplayLicenseKey('REGK-ABCDE-12345')).toBe(false);
  });

  it('accepts billing format REGK-yyyyMMdd-slug-random', () => {
    expect(isValidBillingLicenseKey('REGK-20261231-cafe-A7F3K2D9')).toBe(true);
    expect(isValidBillingLicenseKey('REGK-20260601-my-cafe-shop-A7F3K2D9')).toBe(true);
    expect(isValidBillingLicenseKey('REGK-20261331-cafe-A7F3K2D9')).toBe(false); // invalid date
    expect(isValidBillingLicenseKey('REGK-20261231-admin-A7F3K2D9')).toBe(false); // reserved slug
    expect(isValidBillingLicenseKey('REGK-ABCDE-BBBBB-CCCCC')).toBe(false);
  });

  it('detectLicenseKeyKind distinguishes formats', () => {
    expect(detectLicenseKeyKind('REGK-20261231-dev-A7F3K2D9')).toBe('billing');
    expect(detectLicenseKeyKind('REGK-AAAAA-BBBBB-CCCCC')).toBe('display');
    expect(detectLicenseKeyKind('nope')).toBe('none');
    expect(isValidLicenseKey('REGK-AAAAA-BBBBB-CCCCC')).toBe(true);
  });

  it('normalizes input to upper trim', () => {
    expect(normalizeLicenseKeyInput('  regk-aaaaa-bbbbb-ccccc  ')).toBe('REGK-AAAAA-BBBBB-CCCCC');
  });

  it('createLicenseKeyFormRules enforces billing mode', async () => {
    const rules = createLicenseKeyFormRules({ required: 'req', invalid: 'invalid' }, 'billing');
    const validator = (
      rules.find((r) => typeof r === 'object' && r && 'validator' in r) as {
        validator: (_: unknown, v: string) => Promise<void>;
      }
    ).validator;
    await expect(validator(undefined, 'REGK-AAAAA-BBBBB-CCCCC')).rejects.toThrow('invalid');
    await expect(validator(undefined, 'REGK-20261231-cafe-A7F3K2D9')).resolves.toBeUndefined();
  });
});
