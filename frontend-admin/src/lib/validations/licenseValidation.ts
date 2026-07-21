/**
 * License key format validation.
 *
 * Two backend formats exist:
 * - Display / issued key: `REGK-XXXXX-XXXXX-XXXXX` (RegkTenantLicenseKeyFormat / LicenseService)
 * - Billing / mandant extend: `REGK-{yyyyMMdd}-{tenantSlug}-{8-char}` (LicenseKeyGenerator)
 *
 * Mandant extend UI should prefer billing; Super Admin edit may accept display keys.
 */
import type { Rule } from 'antd/es/form';

import { validateTenantSlug } from '@/features/super-admin/lib/tenantSlug';

/** Deployment / display key — backend RegkTenantLicenseKeyFormat. */
export const LICENSE_KEY_DISPLAY_PATTERN = /^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$/i;

const BILLING_RANDOM_SUFFIX = /^[A-Z0-9]{8}$/i;

export type LicenseKeyKind = 'display' | 'billing' | 'none';

export function normalizeLicenseKeyInput(value: string | undefined | null): string {
  return (value ?? '').trim().toUpperCase();
}

export function isValidDisplayLicenseKey(value: string | undefined | null): boolean {
  const key = (value ?? '').trim();
  return LICENSE_KEY_DISPLAY_PATTERN.test(key);
}

/**
 * Billing mandant key: REGK-yyyyMMdd-{slug}-{8 alnum}.
 * Mirrors LicenseKeyGenerator.ValidateLicenseKeyFormat (case-insensitive REGK prefix).
 */
export function isValidBillingLicenseKey(value: string | undefined | null): boolean {
  const key = (value ?? '').trim();
  if (!key) return false;

  const parts = key.split('-');
  if (parts.length < 4) return false;
  if (!parts[0] || parts[0].toUpperCase() !== 'REGK') return false;

  const datePart = parts[1] ?? '';
  if (datePart.length !== 8 || !/^\d{8}$/.test(datePart)) return false;
  const y = Number(datePart.slice(0, 4));
  const m = Number(datePart.slice(4, 6));
  const d = Number(datePart.slice(6, 8));
  const dt = new Date(Date.UTC(y, m - 1, d));
  if (
    Number.isNaN(dt.getTime()) ||
    dt.getUTCFullYear() !== y ||
    dt.getUTCMonth() !== m - 1 ||
    dt.getUTCDate() !== d
  ) {
    return false;
  }

  const randomPart = parts[parts.length - 1] ?? '';
  if (!BILLING_RANDOM_SUFFIX.test(randomPart)) return false;

  const slug = parts.slice(2, -1).join('-').toLowerCase();
  return validateTenantSlug(slug) === null;
}

/** Accepts either known production key shape. */
export function isValidLicenseKey(value: string | undefined | null): boolean {
  return isValidDisplayLicenseKey(value) || isValidBillingLicenseKey(value);
}

export function detectLicenseKeyKind(value: string | undefined | null): LicenseKeyKind {
  if (isValidBillingLicenseKey(value)) return 'billing';
  if (isValidDisplayLicenseKey(value)) return 'display';
  return 'none';
}

export type LicenseKeyRuleMessages = {
  required: string;
  invalid: string;
};

export type LicenseKeyRuleMode = 'billing' | 'display' | 'any';

/** Ant Design rules for license key fields. */
export function createLicenseKeyFormRules(
  messages: LicenseKeyRuleMessages,
  mode: LicenseKeyRuleMode = 'any'
): Rule[] {
  const check =
    mode === 'billing'
      ? isValidBillingLicenseKey
      : mode === 'display'
        ? isValidDisplayLicenseKey
        : isValidLicenseKey;

  return [
    { required: true, message: messages.required },
    {
      validator: async (_: unknown, value: string | undefined) => {
        const trimmed = (value ?? '').trim();
        if (!trimmed) return;
        if (!check(trimmed)) {
          throw new Error(messages.invalid);
        }
      },
    },
  ];
}
