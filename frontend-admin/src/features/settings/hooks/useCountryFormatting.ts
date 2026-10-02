'use client';

import { useMemo } from 'react';

import { useCompanySettings } from '@/features/settings/hooks/useCompanySettings';
import { useCountries } from '@/features/tenancy/hooks/useCountries';
import {
  type CountryDateInput,
  type CountryFormatProfile,
  formatCountryCurrency,
  formatCountryDate,
  formatCountryDateTime,
  formatCountryNumber,
  formatCountryTime,
  resolveCountryFormatProfile,
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
 * Tenant-facing date / money formatters.
 * Currency and locale come from `GET /api/admin/countries` when that catalog is loaded.
 * Otherwise {@link resolveCountryFormatProfile} uses the local fallback map.
 */
export function useCountryFormatting(): CountryFormatting {
  const settingsQuery = useCompanySettings();
  const countriesQuery = useCountries();
  const country = settingsQuery.data?.country;
  const catalog = countriesQuery.isError ? null : countriesQuery.data;

  const profile = useMemo(
    () => resolveCountryFormatProfile(country, catalog),
    [country, catalog]
  );

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
