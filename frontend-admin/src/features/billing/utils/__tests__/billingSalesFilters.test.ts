import { describe, expect, it } from 'vitest';

import {
  DEFAULT_LICENSE_SALES_SORT_BY,
  DEFAULT_LICENSE_SALES_SORT_DIR,
  LICENSE_SALES_TWO_YEAR_MIN_DAYS,
  billingSalesFiltersToSearchParams,
  countActiveBillingSalesFilters,
  getLicenseSalesAntSortOrder,
  parseBillingSalesFiltersFromSearchParams,
  toBillingSalesListApiParams,
} from '@/features/billing/utils/billingSalesFilters';

describe('billingSalesFilters', () => {
  it('counts active filters', () => {
    expect(
      countActiveBillingSalesFilters({
        page: 1,
        pageSize: 20,
        status: 'all',
        plan: 'all',
      })
    ).toBe(0);

    expect(
      countActiveBillingSalesFilters({
        page: 1,
        pageSize: 20,
        status: 'expired',
        plan: 'oneYear',
        search: 'RE-1',
        tenantId: 't1',
      })
    ).toBe(4);
  });

  it('maps UI filters to API params with default sort', () => {
    expect(
      toBillingSalesListApiParams({
        page: 2,
        pageSize: 20,
        status: 'revoked',
        plan: 'trial',
        search: ' KEY ',
      })
    ).toEqual({
      page: 2,
      pageSize: 20,
      tenantId: undefined,
      search: 'KEY',
      fromDate: undefined,
      toDate: undefined,
      status: 'revoked',
      licenseType: 'Trial',
      sortBy: DEFAULT_LICENSE_SALES_SORT_BY,
      sortDir: DEFAULT_LICENSE_SALES_SORT_DIR,
    });

    expect(
      toBillingSalesListApiParams({
        page: 1,
        pageSize: 20,
        plan: 'twoYears',
        sortBy: 'priceGross',
        sortDir: 'desc',
      })
    ).toMatchObject({
      minDurationDays: LICENSE_SALES_TWO_YEAR_MIN_DAYS,
      sortBy: 'priceGross',
      sortDir: 'desc',
    });

    expect(
      toBillingSalesListApiParams({
        page: 1,
        pageSize: 20,
        plan: 'oneYear',
      })
    ).toMatchObject({
      licensePlan: '12_months',
    });
  });

  it('maps ant sort order and URL params', () => {
    expect(getLicenseSalesAntSortOrder('validUntilUtc', 'validUntilUtc', 'asc')).toBe('ascend');
    expect(getLicenseSalesAntSortOrder('soldAtUtc', 'validUntilUtc', 'asc')).toBeNull();

    const parsed = parseBillingSalesFiltersFromSearchParams(
      new URLSearchParams('sortBy=soldAtUtc&sortDir=desc&tenantId=abc')
    );
    expect(parsed).toMatchObject({
      sortBy: 'soldAtUtc',
      sortDir: 'desc',
      tenantId: 'abc',
    });

    const qs = billingSalesFiltersToSearchParams({
      page: 1,
      pageSize: 20,
      sortBy: 'invoiceNumber',
      sortDir: 'asc',
      status: 'active',
    }).toString();
    expect(qs).toContain('sortBy=invoiceNumber');
    expect(qs).toContain('sortDir=asc');
    expect(qs).toContain('status=active');
  });
});
