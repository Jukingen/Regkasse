/**
 * Shared validation primitives for frontend-admin forms.
 * Aligns with AGENTS.md / backend contracts (username, email, ATU tax number).
 */
import type { Rule } from 'antd/es/form';

/** Austrian UID (ATU + 8 digits) — AGENTS.md / backend tax-number contract. */
export const ATU_TAX_NUMBER_PATTERN = /^ATU\d{8}$/;

/**
 * Username: 3–50 chars, a-z / 0-9 / _ / - (case-insensitive identity).
 * AGENTS.md: `^[a-zA-Z0-9_-]{3,50}$`
 */
export const USERNAME_PATTERN = /^[a-zA-Z0-9_-]{3,50}$/;

/** Character class only (length enforced via min/max rules). */
export const USERNAME_CHAR_PATTERN = /^[a-zA-Z0-9_-]+$/;

export const USERNAME_MIN_LENGTH = 3;
export const USERNAME_MAX_LENGTH = 50;

/** Simple email check aligned with typical .NET EmailAddress usage. */
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export type ValidationTranslate = (
  key: string,
  options?: Record<string, string | number>
) => string;

/** Ant Design max-length validator. */
export function maxLengthRule(max: number, message: string): Rule {
  return {
    validator: (_: unknown, value: string | undefined) =>
      value == null || value.length <= max ? Promise.resolve() : Promise.reject(new Error(message)),
  };
}

export function isValidEmail(value: string | undefined | null): boolean {
  if (value == null || value.trim() === '') return true;
  return EMAIL_PATTERN.test(value.trim());
}

export function isValidUsername(value: string | undefined | null): boolean {
  if (value == null) return false;
  return USERNAME_PATTERN.test(value.trim());
}

export function isValidAtuTaxNumber(value: string | undefined | null): boolean {
  if (value == null) return false;
  return ATU_TAX_NUMBER_PATTERN.test(value.trim());
}
