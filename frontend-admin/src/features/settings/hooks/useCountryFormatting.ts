'use client';

import { useMemo } from 'react';

import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import {
  type CountryDateInput,
  type CountryFormatProfile,
  formatCountryCurrency,
  formatCountryDate,
  formatCountryDateTime,
  formatCountryNumber,
  formatCountryTime,
  getCountryFormatProfile,
} from '@/lib/countryFormatProfiles';

export type CountryFormatting = {
  profile: CountryFormatProfile;
  formatDate: (input: CountryDateInput) => string;
  formatDateTime: (input: CountryDateInput, options?: { includeSeconds?: boolean }) => string;
  formatTime: (input: CountryDateInput, options?: { includeSeconds?: boolean }) => string;
  formatCurrency: (value: number) => string;
  formatNumber: (value: number, fractionDigits?: number) => string;
  formatDecimalSeparator: () => string;
  formatThousandsSeparator: () => string;
};

/**
 * Tenant-facing date / money formatters from CompanySettings.Country.
 * UI language stays on I18nProvider. Unknown / missing country → AT.
 */
export function useCountryFormatting(): CountryFormatting {
  const settingsQuery = useCompanySettings();
  const country = settingsQuery.data?.country;

  const profile = useMemo(() => getCountryFormatProfile(country), [country]);

  return useMemo(
    () => ({
      profile,
      formatDate: (input) => formatCountryDate(input, profile),
      formatDateTime: (input, options) => formatCountryDateTime(input, profile, options),
      formatTime: (input, options) => formatCountryTime(input, profile, options),
      formatCurrency: (value) => formatCountryCurrency(value, profile),
      formatNumber: (value, fractionDigits) =>
        formatCountryNumber(value, profile, fractionDigits ?? 2),
      formatDecimalSeparator: () => profile.decimalSeparator,
      formatThousandsSeparator: () => profile.thousandsSeparator,
    }),
    [profile]
  );
}
