/**
 * Password policy — mirrors ASP.NET Identity options in backend Program.cs:
 *   RequiredLength = 8
 *   RequireDigit / RequireLowercase / RequireUppercase / RequireNonAlphanumeric = true
 */
import type { Rule } from 'antd/es/form';

import { maxLengthRule } from '@/lib/validations/common';

export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

export const PASSWORD_POLICY = {
  minLength: PASSWORD_MIN_LENGTH,
  maxLength: PASSWORD_MAX_LENGTH,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
} as const;

export const PASSWORD_REQUIREMENT_KEYS = [
  'minLength',
  'uppercase',
  'lowercase',
  'digit',
  'special',
] as const;

export type PasswordRequirementKey = (typeof PASSWORD_REQUIREMENT_KEYS)[number];

const PASSWORD_REQUIREMENT_TESTS: Record<PasswordRequirementKey, (password: string) => boolean> = {
  minLength: (password) => password.length >= PASSWORD_MIN_LENGTH,
  uppercase: (password) => /[A-Z]/.test(password),
  lowercase: (password) => /[a-z]/.test(password),
  digit: (password) => /\d/.test(password),
  special: (password) => /[^a-zA-Z0-9]/.test(password),
};

export function getMetPasswordRequirementKeys(password: string): PasswordRequirementKey[] {
  return PASSWORD_REQUIREMENT_KEYS.filter((key) => PASSWORD_REQUIREMENT_TESTS[key](password));
}

export function allPasswordRequirementsMet(password: string): boolean {
  return getMetPasswordRequirementKeys(password).length === PASSWORD_REQUIREMENT_KEYS.length;
}

/** First policy violation message or null if composition is OK (length handled separately). */
export function getPasswordPolicyError(
  value: string | undefined | null,
  policyMessage: string
): string | null {
  if (value == null || value.length === 0) return null;
  if (value.length < PASSWORD_POLICY.minLength) return null;
  if (PASSWORD_POLICY.requireDigit && !/\d/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireLowercase && !/[a-z]/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireUppercase && !/[A-Z]/.test(value)) return policyMessage;
  if (PASSWORD_POLICY.requireNonAlphanumeric && !/[^A-Za-z0-9]/.test(value)) return policyMessage;
  return null;
}

export function isPasswordPolicySatisfied(value: string | undefined | null): boolean {
  if (value == null || value.length === 0) return false;
  if (value.length < PASSWORD_MIN_LENGTH || value.length > PASSWORD_MAX_LENGTH) return false;
  return getPasswordPolicyError(value, 'x') === null;
}

export type PasswordRuleMessages = {
  required: string;
  min: string;
  max: string;
  policy: string;
};

/** Ant Design rules for create / reset / change-password fields. */
export function createPasswordFormRules(messages: PasswordRuleMessages): Rule[] {
  return [
    { required: true, message: messages.required },
    { min: PASSWORD_MIN_LENGTH, message: messages.min },
    maxLengthRule(PASSWORD_MAX_LENGTH, messages.max),
    {
      validator: (_: unknown, value: string | undefined) => {
        const err = getPasswordPolicyError(value, messages.policy);
        return err ? Promise.reject(new Error(err)) : Promise.resolve();
      },
    },
  ];
}

export type PasswordErrorTranslate = (
  key: string,
  options?: Record<string, string | number>
) => string;

/** Maps backend Identity password errors to localized FA messages. */
export function mapBackendPasswordError(t: PasswordErrorTranslate, backendMessage: string): string {
  const lower = backendMessage.toLowerCase();
  if (lower.includes('at least') && lower.includes('character')) {
    return t('users.passwordErrors.minLength', { min: PASSWORD_MIN_LENGTH });
  }
  if (lower.includes('digit') || lower.includes('number')) return t('users.passwordErrors.digit');
  if (lower.includes('lowercase') || lower.includes('lower case')) {
    return t('users.passwordErrors.lowercase');
  }
  if (lower.includes('uppercase') || lower.includes('upper case')) {
    return t('users.passwordErrors.uppercase');
  }
  if (
    lower.includes('non-alphanumeric') ||
    lower.includes('non alphanumeric') ||
    lower.includes('special')
  ) {
    return t('users.passwordErrors.nonAlphanumeric');
  }
  return t('users.passwordErrors.generic');
}
