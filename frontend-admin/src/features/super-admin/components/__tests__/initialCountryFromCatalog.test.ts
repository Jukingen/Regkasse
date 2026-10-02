import { describe, expect, it } from 'vitest';

import { initialCountryFromCatalog } from '@/features/super-admin/components/CreateTenantWizard';

/** Stand-in for `GET /api/admin/countries`. */
const COUNTRIES_API = [
  {
    code: 'DE',
    name: 'Germany',
    allowedVatRegimes: ['DE_USTG_STANDARD', 'DE_KLEINUNTERNEHMER'],
  },
  {
    code: 'AT',
    name: 'Austria',
    allowedVatRegimes: ['AT_RKSV_STANDARD'],
  },
  {
    code: 'CH',
    name: 'Switzerland',
    allowedVatRegimes: ['CH_MWST_STANDARD'],
  },
];

describe('initialCountryFromCatalog', () => {
  it('seeds the wizard from the first countries API row, not a fixed AT code', () => {
    expect(initialCountryFromCatalog(COUNTRIES_API)).toEqual({
      countryCode: 'DE',
      vatRegime: 'DE_USTG_STANDARD',
    });
  });

  it('leaves the country empty when the countries API has not returned', () => {
    expect(initialCountryFromCatalog([])).toEqual({});
  });
});
