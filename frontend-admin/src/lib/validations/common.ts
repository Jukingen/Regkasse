/**
 * Shared validation primitives for frontend-admin forms.
 * VAT-ID shapes come from `GET /api/admin/countries` (`vatIdPattern`), not from local literals.
 */
import type { Rule } from 'antd/es/form';

/** One row of the country catalog (`CountryProfileSummaryDto`). */
export type CountryVatIdProfile = {
  code?: string | null;
  vatIdPattern?: string | null;
};

/**
 * Compiles a VAT-ID regex supplied by the country catalog.
 * Blank or invalid patterns yield null so callers do not invent a local shape.
 */
export function compileVatIdPattern(pattern: string | null | undefined): RegExp | null {
  const source = pattern?.trim();
  if (!source) return null;
  try {
    return new RegExp(source);
  } catch {
    return null;
  }
}

/**
 * VAT-ID pattern for `country` from a `GET /api/admin/countries` payload.
 * Unknown country or a missing catalog returns null.
 */
export function vatIdPatternFromCatalog(
  country: string | null | undefined,
  profiles: readonly CountryVatIdProfile[] | null | undefined
): RegExp | null {
  const code = country?.trim().toUpperCase();
  if (!code || !profiles?.length) return null;
  const match = profiles.find((profile) => profile.code?.trim().toUpperCase() === code);
  return compileVatIdPattern(match?.vatIdPattern);
}

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

export function isValidVatId(
  value: string | undefined | null,
  pattern: RegExp | string | null | undefined
): boolean {
  if (value == null) return false;
  const compiled = typeof pattern === 'string' ? compileVatIdPattern(pattern) : pattern;
  if (!compiled) return false;
  return compiled.test(value.trim());
}
