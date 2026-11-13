import { describe, expect, it } from 'vitest';

import { countrySelectOptionsFromCatalog } from '@/features/tenants/components/TenantSettingsChangePanel';

/** Stand-in for `GET /api/admin/countries`. */
const COUNTRIES_API = [
  { code: 'AT', name: 'Austria' },
  { code: 'DE', name: 'Germany' },
  { code: 'CH', name: 'Switzerland' },
];

describe('countrySelectOptionsFromCatalog', () => {
  it('lists AT, DE, and CH from the countries API payload', () => {
    const options = countrySelectOptionsFromCatalog(COUNTRIES_API, 'AT', false);
    expect(options.map((option) => option.value)).toEqual(['AT', 'DE', 'CH']);
    expect(options.map((option) => option.label)).toEqual([
      'AT — Austria',
      'DE — Germany',
      'CH — Switzerland',
    ]);
  });

  it('does not invent AT/DE options when the countries API is unavailable', () => {
    expect(countrySelectOptionsFromCatalog([], 'CH', false)).toEqual([
      { value: 'CH', label: 'CH', disabled: false },
    ]);
    expect(countrySelectOptionsFromCatalog([], undefined, false)).toEqual([]);
  });
});
