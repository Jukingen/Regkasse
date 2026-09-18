/**
 * Shared validation primitives for frontend-admin forms.
 * Aligns with AGENTS.md / backend contracts (username, email, ATU tax number).
 */
import type { Rule } from 'antd/es/form';

/** Austrian UID (ATU + 8 digits) — AGENTS.md / backend tax-number contract. */
export const ATU_TAX_NUMBER_PATTERN = /^ATU\d{8}$/;

/**
 * Per-country VAT-ID shapes. Must stay byte-identical to
 * `backend/Models/Countries/VatIdPatterns` (backend remains the source of truth).
 */
export const VAT_ID_PATTERNS = {
  AT: ATU_TAX_NUMBER_PATTERN,
  DE: /^DE\d{9}$/,
  CH: /^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$/,
} as const;

export type VatIdCountryCode = keyof typeof VAT_ID_PATTERNS;

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

export function normalizeVatIdCountry(country?: string | null): VatIdCountryCode {
  const code = (country ?? 'AT').trim().toUpperCase();
  if (code === 'DE' || code === 'CH') return code;
  return 'AT';
}

export function vatIdPatternForCountry(country?: string | null): RegExp {
  return VAT_ID_PATTERNS[normalizeVatIdCountry(country)];
}

export function isValidVatId(
  value: string | undefined | null,
  country?: string | null
): boolean {
  if (value == null) return false;
  return vatIdPatternForCountry(country).test(value.trim());
}

export function vatIdInvalidMessageKey(country?: string | null): string {
  const code = normalizeVatIdCountry(country);
  if (code === 'DE') return 'superadmin.validation.vatId.invalid.DE';
  if (code === 'CH') return 'superadmin.validation.vatId.invalid.CH';
  return 'common.validation.atuTaxNumberPattern';
}
