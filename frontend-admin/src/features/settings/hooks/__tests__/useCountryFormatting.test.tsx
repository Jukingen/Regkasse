import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useCountryFormatting } from '@/features/settings/hooks/useCountryFormatting';

const settingsState = vi.hoisted(() => ({
  country: 'AT' as string | undefined,
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => ({
    data: settingsState.country == null ? undefined : { country: settingsState.country },
  }),
}));

describe('useCountryFormatting', () => {
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
});
