import { describe, expect, it } from 'vitest';

import { isPasswordPolicySatisfied } from '@/lib/validations/passwordValidation';
import {
  LOGIN_USERNAME_PATTERN,
  NAME_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  buildUsersFormRulesContext,
  createLoginUserNameRules,
  createUsersFormRules,
  isValidEmail,
} from '@/lib/validations/userValidation';

const t = (key: string, options?: Record<string, string | number>) =>
  options ? `${key}:${JSON.stringify(options)}` : key;

async function runRule(rules: unknown[], value: unknown) {
  for (const rule of rules) {
    if (!rule || typeof rule !== 'object') continue;
    const r = rule as {
      required?: boolean;
      min?: number;
      max?: number;
      pattern?: RegExp;
      type?: string;
      message?: string;
      validator?: (_: unknown, v: unknown) => Promise<void>;
    };
    if (r.required && (value == null || value === '')) {
      throw new Error(r.message ?? 'required');
    }
    if (typeof value === 'string' && typeof r.min === 'number' && value.length < r.min) {
      throw new Error(r.message ?? 'min');
    }
    if (typeof value === 'string' && typeof r.max === 'number' && value.length > r.max) {
      throw new Error(r.message ?? 'max');
    }
    if (typeof value === 'string' && r.pattern && !r.pattern.test(value)) {
      throw new Error(r.message ?? 'pattern');
    }
    if (r.validator) {
      await r.validator(undefined, value);
    }
  }
}

describe('userValidation', () => {
  it('enforces AGENTS.md username character class', () => {
    expect(LOGIN_USERNAME_PATTERN.test('manager1')).toBe(true);
    expect(LOGIN_USERNAME_PATTERN.test('bad name')).toBe(false);
    expect(LOGIN_USERNAME_PATTERN.test('ab')).toBe(true); // length via min/max rules
  });

  it('createLoginUserNameRules rejects short and invalid usernames', async () => {
    const rules = createLoginUserNameRules({
      required: 'req',
      min: 'min',
      max: 'max',
      pattern: 'pat',
    });
    await expect(runRule(rules, '')).rejects.toThrow('req');
    await expect(runRule(rules, 'ab')).rejects.toThrow('min');
    await expect(runRule(rules, 'bad name')).rejects.toThrow('pat');
    await expect(runRule(rules, 'manager1')).resolves.toBeUndefined();
  });

  it('createUsersFormRules enforces Identity password policy', async () => {
    const rules = createUsersFormRules(buildUsersFormRulesContext(t));
    await expect(runRule(rules.password, 'short')).rejects.toThrow();
    await expect(runRule(rules.password, 'Password1')).rejects.toThrow(); // missing special
    await expect(runRule(rules.password, 'Password1!')).resolves.toBeUndefined();
    expect(isPasswordPolicySatisfied('Password1!')).toBe(true);
    expect(PASSWORD_MIN_LENGTH).toBe(8);
  });

  it('limits names to backend NAME_MAX_LENGTH', async () => {
    const rules = createUsersFormRules(buildUsersFormRulesContext(t));
    const tooLong = 'x'.repeat(NAME_MAX_LENGTH + 1);
    await expect(runRule(rules.firstName, tooLong)).rejects.toThrow();
    await expect(runRule(rules.firstName, 'Max')).resolves.toBeUndefined();
  });

  it('treats empty email as valid for optional create fields', () => {
    expect(isValidEmail('')).toBe(true);
    expect(isValidEmail('bad')).toBe(false);
    expect(isValidEmail('a@b.co')).toBe(true);
  });
});
