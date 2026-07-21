import { describe, expect, it } from 'vitest';

import {
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  allPasswordRequirementsMet,
  createPasswordFormRules,
  getMetPasswordRequirementKeys,
  getPasswordPolicyError,
  isPasswordPolicySatisfied,
  mapBackendPasswordError,
} from '@/lib/validations/passwordValidation';

describe('passwordValidation', () => {
  it('matches backend Identity length bounds', () => {
    expect(PASSWORD_MIN_LENGTH).toBe(8);
    expect(PASSWORD_MAX_LENGTH).toBe(128);
  });

  it('reports missing composition requirements', () => {
    expect(getPasswordPolicyError('abcdefg1', 'policy')).toBe('policy'); // no upper/special
    expect(getPasswordPolicyError('ABCDEFG1!', 'policy')).toBe('policy'); // no lower
    expect(getPasswordPolicyError('Abcdefgh!', 'policy')).toBe('policy'); // no digit
    expect(getPasswordPolicyError('Abcdefg1', 'policy')).toBe('policy'); // no special
    expect(getPasswordPolicyError('Abcdefg1!', 'policy')).toBeNull();
  });

  it('lists met requirement keys', () => {
    expect(getMetPasswordRequirementKeys('Ab1!xxxx').sort()).toEqual(
      ['digit', 'lowercase', 'minLength', 'special', 'uppercase'].sort()
    );
    expect(allPasswordRequirementsMet('short')).toBe(false);
    expect(isPasswordPolicySatisfied('Ab1!xxxx')).toBe(true);
  });

  it('maps backend Identity error phrases', () => {
    const t = (key: string) => key;
    expect(mapBackendPasswordError(t, 'Passwords must have at least one digit.')).toBe(
      'users.passwordErrors.digit'
    );
    expect(mapBackendPasswordError(t, 'Passwords must have at least one uppercase.')).toBe(
      'users.passwordErrors.uppercase'
    );
  });

  it('createPasswordFormRules rejects weak passwords', async () => {
    const rules = createPasswordFormRules({
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
  });
});
