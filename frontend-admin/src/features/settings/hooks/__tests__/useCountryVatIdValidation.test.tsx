import { renderHook } from '@testing-library/react';
import type { Rule } from 'antd/es/form';
import { describe, expect, it, vi } from 'vitest';

import { useCountryVatIdValidation } from '@/features/settings/hooks/useCountryVatIdValidation';

/** Stand-in for `GET /api/admin/countries`. */
const COUNTRIES_API = [
  { code: 'AT', vatIdPattern: String.raw`^ATU\d{8}$` },
  { code: 'DE', vatIdPattern: String.raw`^DE\d{9}$` },
  { code: 'CH', vatIdPattern: String.raw`^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$` },
];

const countriesState = vi.hoisted(() => ({
  data: undefined as typeof COUNTRIES_API | undefined,
  isError: false,
}));

vi.mock('@/features/tenancy/hooks/useCountries', () => ({
  useCountries: () => countriesState,
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => ({ data: undefined }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
  }),
}));

async function runValidator(rules: Rule[], value: string) {
  const shape = rules.find((rule) => 'validator' in rule) as
    | { validator?: (_: unknown, value: unknown) => Promise<void> }
    | undefined;
  await shape?.validator?.(undefined, value);
}

describe('useCountryVatIdValidation', () => {
  it('accepts AT, DE, and CH values using the countries API pattern', async () => {
    countriesState.data = COUNTRIES_API;
    countriesState.isError = false;

    const at = renderHook(() => useCountryVatIdValidation({ country: 'AT', required: false }));
    await expect(runValidator(at.result.current.rules, 'ATU12345678')).resolves.toBeUndefined();
    await expect(runValidator(at.result.current.rules, 'DE123456789')).rejects.toThrow(
      'common.validation.invalidValue'
    );
    expect(at.result.current.isValid('ATU12345678')).toBe(true);

    const de = renderHook(() => useCountryVatIdValidation({ country: 'DE', required: false }));
    await expect(runValidator(de.result.current.rules, 'DE123456789')).resolves.toBeUndefined();
    await expect(runValidator(de.result.current.rules, 'DE12345678')).rejects.toThrow(
      'common.validation.invalidValue'
    );

    const ch = renderHook(() => useCountryVatIdValidation({ country: 'CH', required: false }));
    await expect(runValidator(ch.result.current.rules, 'CHE-123.456.789 MWST')).resolves.toBeUndefined();
    await expect(runValidator(ch.result.current.rules, 'CHE-123.456.78')).rejects.toThrow(
      'common.validation.invalidValue'
    );
  });

  it('does not apply a local regex when the countries API is unavailable', async () => {
    countriesState.data = undefined;
    countriesState.isError = true;
    const { result } = renderHook(() =>
      useCountryVatIdValidation({ country: 'AT', required: false })
    );
    expect(result.current.pattern).toBeNull();
    expect(result.current.rules.some((rule) => 'validator' in rule)).toBe(false);
  });
});
