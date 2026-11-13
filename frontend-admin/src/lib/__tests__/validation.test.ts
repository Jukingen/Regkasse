import { describe, expect, it } from 'vitest';

import { USERNAME_PATTERN, createValidationRules } from '@/lib/validation';
import { vatIdPatternFromCatalog } from '@/lib/validations/common';

/** Stand-in for `GET /api/admin/countries`. */
const COUNTRIES_API = [
  { code: 'AT', vatIdPattern: String.raw`^ATU\d{8}$` },
  { code: 'DE', vatIdPattern: String.raw`^DE\d{9}$` },
  { code: 'CH', vatIdPattern: String.raw`^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$` },
];

function t(key: string, options?: Record<string, string | number>): string {
  if (options) {
    return `${key}:${JSON.stringify(options)}`;
  }
  return key;
}

describe('createValidationRules', () => {
  const rules = createValidationRules(t);

  it('builds required rule with field interpolation', () => {
    expect(rules.required('Name')).toEqual({
      required: true,
      message: 'common.validation.requiredWithField:{"field":"Name"}',
    });
  });

  it('exposes email type rule', () => {
    expect(rules.email).toEqual({
      type: 'email',
      message: 'common.validation.emailInvalid',
    });
  });

  it('builds min/max length rules', () => {
    expect(rules.min(3)).toEqual({
      min: 3,
      message: 'common.validation.minLength:{"min":3}',
    });
    expect(rules.max(50)).toEqual({
      max: 50,
      message: 'common.validation.maxLength:{"max":50}',
    });
  });

  it('builds pattern rule with custom message', () => {
    const pattern = /^abc$/;
    expect(rules.pattern(pattern, 'custom')).toEqual({ pattern, message: 'custom' });
  });

  it('builds optional VAT rules from the countries catalog pattern', async () => {
    const atPattern = COUNTRIES_API.find((row) => row.code === 'AT')?.vatIdPattern;
    const optional = rules.atuTaxNumber(atPattern, false);
    expect(optional).toHaveLength(1);
    const rule = optional[0] as { validator?: (_: unknown, value: unknown) => Promise<void> };
    await expect(rule.validator?.(undefined, '')).resolves.toBeUndefined();
    await expect(rule.validator?.(undefined, 'ATU12345678')).resolves.toBeUndefined();
    await expect(rule.validator?.(undefined, 'bad')).rejects.toThrow('common.validation.invalidValue');
  });

  it('builds required username rules', () => {
    const username = rules.username();
    expect(username.some((r) => 'required' in r && r.required)).toBe(true);
    expect(username.some((r) => 'pattern' in r && r.pattern === USERNAME_PATTERN)).toBe(true);
  });
});

describe('shared patterns', () => {
  it('validates AT, DE, and CH with the countries API patterns', () => {
    const at = vatIdPatternFromCatalog('AT', COUNTRIES_API);
    const de = vatIdPatternFromCatalog('DE', COUNTRIES_API);
    const ch = vatIdPatternFromCatalog('CH', COUNTRIES_API);
    expect(at?.test('ATU12345678')).toBe(true);
    expect(at?.test('ATU1234567')).toBe(false);
    expect(at?.test('atu12345678')).toBe(false);
    expect(de?.test('DE123456789')).toBe(true);
    expect(de?.test('DE12345678')).toBe(false);
    expect(ch?.test('CHE-123.456.789')).toBe(true);
    expect(ch?.test('CHE-123.456.789 MWST')).toBe(true);
    expect(ch?.test('CHE-123.456.78')).toBe(false);
  });

  it('accepts valid usernames', () => {
    expect(USERNAME_PATTERN.test('manager1')).toBe(true);
    expect(USERNAME_PATTERN.test('ab')).toBe(false);
    expect(USERNAME_PATTERN.test('bad name')).toBe(false);
  });
});
