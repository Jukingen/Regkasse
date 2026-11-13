import { describe, expect, it } from 'vitest';

import { isPasswordPolicySatisfied } from '@/lib/validations/passwordValidation';
import {
  TENANT_COMPANY_NAME_MAX,
  TENANT_COMPANY_NAME_MIN,
  createTenantAdminPasswordRules,
  isOptionalFieldValid,
  validateAddress,
  validateCompanyName,
  validateContactEmail,
  validatePhone,
} from '@/lib/validations/tenantValidation';

describe('tenantValidation', () => {
  describe('validateCompanyName', () => {
    it('requires non-empty name', () => {
      expect(validateCompanyName('')).toBe('required');
      expect(validateCompanyName('  ')).toBe('required');
    });

    it('rejects too short / too long names', () => {
      expect(validateCompanyName('A')).toBe('tooShort');
      expect(validateCompanyName('x'.repeat(TENANT_COMPANY_NAME_MAX + 1))).toBe('tooLong');
      expect(TENANT_COMPANY_NAME_MIN).toBe(2);
    });

    it('accepts valid company names', () => {
      expect(validateCompanyName('Café Beispiel GmbH')).toBeNull();
    });
  });

  describe('validateContactEmail', () => {
    it('requires non-empty email', () => {
      expect(validateContactEmail('')).toBe('required');
    });

    it('accepts / rejects emails', () => {
      expect(validateContactEmail('kontakt@cafe-example.at')).toBeNull();
      expect(validateContactEmail('not-an-email')).toBe('invalid');
    });
  });

  describe('validatePhone / validateAddress', () => {
    it('allows empty optional phone and address', () => {
      expect(validatePhone('')).toBeNull();
      expect(validateAddress('')).toBeNull();
    });

    it('accepts formatted phone numbers', () => {
      expect(validatePhone('+43 1 234 5678')).toBeNull();
      expect(validatePhone('abc')).toBe('invalid');
    });

    it('rejects oversized address', () => {
      expect(validateAddress('x'.repeat(501))).toBe('tooLong');
    });
  });

  it('isOptionalFieldValid requires non-empty valid value', () => {
    expect(isOptionalFieldValid('', validatePhone)).toBe(false);
    expect(isOptionalFieldValid('+431234567', validatePhone)).toBe(true);
  });

  it('createTenantAdminPasswordRules mirrors Identity policy', async () => {
    const rules = createTenantAdminPasswordRules({
      required: 'req',
      min: 'min',
      max: 'max',
      policy: 'policy',
    });
    const validators = rules
      .filter(
        (r): r is { validator: (_: unknown, v: string) => Promise<void> } =>
          typeof r === 'object' && !!r && 'validator' in r && typeof r.validator === 'function'
      )
      .map((r) => r.validator);

    await expect(Promise.all(validators.map((v) => v(undefined, 'Password1')))).rejects.toThrow(
      'policy'
    );
    await expect(Promise.all(validators.map((v) => v(undefined, 'Password1!')))).resolves.toEqual([
      undefined,
      undefined,
    ]);
    expect(isPasswordPolicySatisfied('Password1!')).toBe(true);
  });
});
