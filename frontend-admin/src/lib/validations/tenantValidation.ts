/**
 * Tenant create/edit validation (company contact fields).
 * Slug normalization stays in `@/features/super-admin/lib/tenantSlug` (UI keystroke helpers).
 */
import { EMAIL_PATTERN } from '@/lib/validations/common';
import {
  PASSWORD_MIN_LENGTH,
  type PasswordRuleMessages,
  createPasswordFormRules,
} from '@/lib/validations/passwordValidation';

const COMPANY_NAME_MIN = 2;
const COMPANY_NAME_MAX = 200;
const ADDRESS_MAX = 500;

export const TENANT_COMPANY_NAME_MIN = COMPANY_NAME_MIN;
export const TENANT_COMPANY_NAME_MAX = COMPANY_NAME_MAX;
export const TENANT_ADDRESS_MAX = ADDRESS_MAX;

export type CompanyNameValidationCode = 'required' | 'tooShort' | 'tooLong';
export type ContactEmailValidationCode = 'required' | 'invalid';
export type PhoneValidationCode = 'invalid';
export type AddressValidationCode = 'tooLong';

/** Digits, spaces, +, -, parentheses — typical AT/EU phone input. */
const PHONE_PATTERN = /^\+?[\d\s\-()/]{6,30}$/;

export function validateCompanyName(value: string | undefined): CompanyNameValidationCode | null {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return 'required';
  }
  if (trimmed.length < COMPANY_NAME_MIN) {
    return 'tooShort';
  }
  if (trimmed.length > COMPANY_NAME_MAX) {
    return 'tooLong';
  }
  return null;
}

export function validateContactEmail(value: string | undefined): ContactEmailValidationCode | null {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return 'required';
  }
  if (!EMAIL_PATTERN.test(trimmed)) {
    return 'invalid';
  }
  return null;
}

export function validatePhone(value: string | undefined): PhoneValidationCode | null {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return null;
  }
  if (!PHONE_PATTERN.test(trimmed)) {
    return 'invalid';
  }
  return null;
}

export function validateAddress(value: string | undefined): AddressValidationCode | null {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return null;
  }
  if (trimmed.length > ADDRESS_MAX) {
    return 'tooLong';
  }
  return null;
}

/** Optional field: success when user entered a valid non-empty value. */
export function isOptionalFieldValid(
  value: string | undefined,
  validator: (v: string | undefined) => string | null
): boolean {
  const trimmed = value?.trim() ?? '';
  if (!trimmed) {
    return false;
  }
  return validator(value) === null;
}

/**
 * Ant Design rules for tenant wizard admin password (Identity policy).
 * Prefer shared messages from `tenants.create.wizard.fields.*`.
 */
export function createTenantAdminPasswordRules(messages: PasswordRuleMessages) {
  return createPasswordFormRules(messages);
}

export { PASSWORD_MIN_LENGTH };
