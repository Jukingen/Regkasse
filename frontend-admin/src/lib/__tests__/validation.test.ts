import { describe, expect, it } from 'vitest';

import { ATU_TAX_NUMBER_PATTERN, USERNAME_PATTERN, createValidationRules } from '@/lib/validation';

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

  it('builds optional ATU tax rules that allow empty', async () => {
    const optional = rules.atuTaxNumber(false);
    expect(optional).toHaveLength(1);
    const rule = optional[0] as { validator?: (_: unknown, value: unknown) => Promise<void> };
    await expect(rule.validator?.(undefined, '')).resolves.toBeUndefined();
    await expect(rule.validator?.(undefined, 'ATU12345678')).resolves.toBeUndefined();
    await expect(rule.validator?.(undefined, 'bad')).rejects.toThrow(
      'common.validation.atuTaxNumberPattern'
    );
  });

  it('builds required username rules', () => {
    const username = rules.username();
    expect(username.some((r) => 'required' in r && r.required)).toBe(true);
    expect(username.some((r) => 'pattern' in r && r.pattern === USERNAME_PATTERN)).toBe(true);
  });
});

describe('shared patterns', () => {
  it('accepts valid ATU numbers', () => {
    expect(ATU_TAX_NUMBER_PATTERN.test('ATU12345678')).toBe(true);
    expect(ATU_TAX_NUMBER_PATTERN.test('ATU1234567')).toBe(false);
    expect(ATU_TAX_NUMBER_PATTERN.test('atu12345678')).toBe(false);
  });

  it('accepts valid usernames', () => {
    expect(USERNAME_PATTERN.test('manager1')).toBe(true);
    expect(USERNAME_PATTERN.test('ab')).toBe(false);
    expect(USERNAME_PATTERN.test('bad name')).toBe(false);
  });
});
