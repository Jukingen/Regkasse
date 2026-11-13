import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useCountryFormatting } from '@/features/settings/hooks/useCountryFormatting';

const settingsState = vi.hoisted(() => ({
  country: 'AT' as string | undefined,
}));

const countriesState = vi.hoisted(() => ({
  data: undefined as
    | { code: string; currency: string; defaultLocale: string }[]
    | undefined,
  isError: false,
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => ({
    data: settingsState.country == null ? undefined : { country: settingsState.country },
  }),
}));

vi.mock('@/features/tenancy/hooks/useCountries', () => ({
  useCountries: () => countriesState,
}));

describe('useCountryFormatting', () => {
  beforeEach(() => {
    countriesState.data = undefined;
    countriesState.isError = false;
  });

  it('uses AT formatters when company country is AT', () => {
    settingsState.country = 'AT';
    const { result } = renderHook(() => useCountryFormatting());
    expect(result.current.profile.code).toBe('AT');
    expect(result.current.formatDate('2026-01-15')).toBe('15.01.2026');
    expect(result.current.formatCurrency(12.5)).toBe('12,50 €');
    expect(result.current.formatDecimalSeparator()).toBe(',');
    expect(result.current.formatThousandsSeparator()).toBe('.');
  });

  it('uses DE timezone and the same date pattern as AT', () => {
    settingsState.country = 'DE';
    const { result } = renderHook(() => useCountryFormatting());
    expect(result.current.profile.code).toBe('DE');
    expect(result.current.profile.timeZone).toBe('Europe/Berlin');
    expect(result.current.formatDate('2026-01-15')).toBe('15.01.2026');
  });

  it('uses Swiss separators for CH', () => {
    settingsState.country = 'CH';
    const { result } = renderHook(() => useCountryFormatting());
    expect(result.current.profile.code).toBe('CH');
    expect(result.current.formatNumber(1234.56)).toBe("1'234.56");
    expect(result.current.formatThousandsSeparator()).toBe("'");
  });

  it('falls back to AT when CompanySettings is missing', () => {
    settingsState.country = undefined;
    const { result } = renderHook(() => useCountryFormatting());
    expect(result.current.profile.code).toBe('AT');
    expect(result.current.formatDate('2026-01-15')).toBe('15.01.2026');
  });

  it('does not throw for an unknown country code', () => {
    settingsState.country = 'XX';
    expect(() => {
      const { result } = renderHook(() => useCountryFormatting());
      expect(result.current.profile.code).toBe('AT');
      expect(result.current.formatDate('2026-01-15')).toBe('15.01.2026');
    }).not.toThrow();
  });

  it('uses currency and locale from GET /api/admin/countries when the catalog is loaded', () => {
    settingsState.country = 'DE';
    countriesState.data = [
      { code: 'AT', currency: 'EUR', defaultLocale: 'de-DE' },
      { code: 'DE', currency: 'EUR', defaultLocale: 'fr-DE' },
      { code: 'CH', currency: 'CHF', defaultLocale: 'de-CH' },
    ];
    const { result } = renderHook(() => useCountryFormatting());
    expect(result.current.profile.locale).toBe('fr-DE');
    expect(result.current.profile.currency).toBe('EUR');
    expect(result.current.formatCurrency(12.5)).toBe('12,50 €');
    expect(result.current.profile.timeZone).toBe('Europe/Berlin');
  });
});
