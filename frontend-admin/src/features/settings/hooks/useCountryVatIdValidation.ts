'use client';

import type { Rule } from 'antd/es/form';
import { useMemo } from 'react';

import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import { useCountries } from '@/features/tenancy/hooks/useCountries';
import { useI18n } from '@/i18n';
import {
  createCountryVatIdRules,
  isValidVatId,
  vatIdPatternFromCatalog,
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
 * VAT-ID form rules. The regex is `vatIdPattern` from `GET /api/admin/countries`.
 * When that catalog is not loaded, only the required check runs.
 */
export function useCountryVatIdValidation(options?: UseCountryVatIdValidationOptions) {
  const { t } = useI18n();
  const settingsQuery = useCompanySettings();
  const countriesQuery = useCountries();
  const required = options?.required ?? true;
  const requiredMessage = options?.messages?.required;
  const invalidAtMessage = options?.messages?.invalidAt;

  const country = useMemo(() => {
    const raw = options?.country ?? settingsQuery.data?.country ?? '';
    return raw.trim().toUpperCase();
  }, [options?.country, settingsQuery.data?.country]);

  const pattern = useMemo(
    () => vatIdPatternFromCatalog(country, countriesQuery.isError ? null : countriesQuery.data),
    [country, countriesQuery.data, countriesQuery.isError]
  );
  const patternSource = useMemo(() => {
    const code = country;
    const rows = countriesQuery.isError ? null : countriesQuery.data;
    const match = rows?.find((row) => row.code?.trim().toUpperCase() === code);
    return match?.vatIdPattern ?? null;
  }, [country, countriesQuery.data, countriesQuery.isError]);

  const rules = useMemo<Rule[]>(
    () =>
      createCountryVatIdRules(t, patternSource, {
        required,
        requiredMessage,
        invalidAtMessage,
      }),
    [t, patternSource, required, requiredMessage, invalidAtMessage]
  );

  return {
    country,
    pattern,
    rules,
    isValid: (value: string | null | undefined) =>
      pattern ? isValidVatId(value, pattern) : Boolean(value?.trim()),
  };
}
