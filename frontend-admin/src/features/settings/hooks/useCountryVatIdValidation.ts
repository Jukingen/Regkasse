'use client';

import type { Rule } from 'antd/es/form';
import { useMemo } from 'react';

import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import { useI18n } from '@/i18n';
import {
  createCountryVatIdRules,
  isValidVatId,
  vatIdPatternForCountry,
} from '@/lib/validations';

export type UseCountryVatIdValidationOptions = {
  /** Override the tenant country (tenant-settings panel). Defaults to company settings. */
  country?: string | null;
  required?: boolean;
  /** AT copy overrides so existing form strings stay byte-identical. */
  messages?: {
    required?: string;
    invalidAt?: string;
  };
};

/**
 * Country-profile VAT-ID form rules. Reads the tenant `Country` from company settings unless
 * `country` is passed. AT messages stay on the existing keys; DE/CH use
 * `superadmin.validation.vatId.invalid.*`.
 */
export function useCountryVatIdValidation(options?: UseCountryVatIdValidationOptions) {
  const { t } = useI18n();
  const settingsQuery = useCompanySettings();
  const required = options?.required ?? true;
  const requiredMessage = options?.messages?.required;
  const invalidAtMessage = options?.messages?.invalidAt;

  const country = useMemo(() => {
    const raw = options?.country ?? settingsQuery.data?.country ?? 'AT';
    const code = raw.trim().toUpperCase();
    return code || 'AT';
  }, [options?.country, settingsQuery.data?.country]);

  const pattern = vatIdPatternForCountry(country);

  const rules = useMemo<Rule[]>(
    () =>
      createCountryVatIdRules(t, country, {
        required,
        requiredMessage,
        invalidAtMessage,
      }),
    [t, country, required, requiredMessage, invalidAtMessage]
  );

  return {
    country,
    pattern,
    rules,
    isValid: (value: string | null | undefined) => isValidVatId(value, country),
  };
}
