import dayjs from 'dayjs';
import { describe, expect, it } from 'vitest';

import { countActiveProductFilters } from '@/features/products/utils/countActiveProductFilters';
import { productFiltersToApiParams } from '@/features/products/utils/productFiltersToApiParams';
import { countActivePaymentFilters } from '@/features/payments/utils/countActivePaymentFilters';
import { buildAuditLogExportQuery } from '@/features/audit-logs/utils/buildAuditLogExportQuery';
import {
  formatLicensePlanLabel,
  formatSaleStatusLabel,
  isSaleCancellable,
} from '@/features/billing/utils/billingFormatters';
import {
  registerStatusColor,
  tenantStatusColor,
} from '@/features/super-admin/utils/tenantStatusLabel';
import { buildPitrDateTimeConstraints } from '@/features/backup/logic/pitrDateTimeConstraints';
import { LICENSE_SALE_PLAN_VALUES } from '@/features/billing/constants/licensePlans';

describe('countActiveProductFilters / productFiltersToApiParams', () => {
  it('counts only active dimensions', () => {
    expect(countActiveProductFilters({})).toBe(0);
    expect(
      countActiveProductFilters({
        searchTerm: '  tea ',
        stockStatus: 'Low',
        status: 'inactive',
        taxTypes: ['A'],
        isTaxable: true,
      }),
    ).toBe(5);
  });

  it('maps filters to API params with edge cases', () => {
    const params = productFiltersToApiParams(
      {
        searchTerm: 'ab',
        minPrice: 1,
        maxPrice: 9,
        stockStatus: 'All',
        status: 'all',
        isTaxable: false,
        createdRange: [dayjs('2026-01-01'), dayjs('2026-01-31')],
      },
      { page: 2, pageSize: 25 },
    );
    expect(params.page).toBe(2);
    expect(params.searchTerm).toBe('ab');
    expect(params.isActive).toBe('all');
    expect(params.isTaxable).toBe(false);
    expect(params.createdFrom).toBe('2026-01-01');
    expect(params.stockStatus).toBeUndefined();

    const short = productFiltersToApiParams({ searchTerm: 'x' }, { page: 1, pageSize: 10 });
    expect(short.searchTerm).toBeUndefined();
  });
});

describe('countActivePaymentFilters', () => {
  it('counts payment filter dimensions', () => {
    expect(countActivePaymentFilters({})).toBe(0);
    expect(
      countActivePaymentFilters({
        dateRange: [dayjs('2026-01-01'), dayjs('2026-01-02')],
        isStorno: true,
        isRefund: true,
        paymentMethods: ['Cash'],
      }),
    ).toBe(4);
  });
});

describe('buildAuditLogExportQuery', () => {
  it('serializes dates and flags', () => {
    const q = buildAuditLogExportQuery({
      startDate: '2026-01-01',
      endDate: '2026-01-31',
      action: 'Login',
      hasChanges: true,
    });
    expect(q.action).toBe('Login');
    expect(q.hasChanges).toBe('true');
    expect(q.startDate).toBeTruthy();
    expect(buildAuditLogExportQuery({ hasChanges: false }).hasChanges).toBe('false');
  });
});

describe('billingFormatters', () => {
  const t = (key: string) => key;
  it('maps plan and status labels', () => {
    expect(formatLicensePlanLabel(LICENSE_SALE_PLAN_VALUES.sixMonths, t)).toBe(
      'billing.plans.sixMonths',
    );
    expect(formatLicensePlanLabel('other', t)).toBe('other');
    expect(formatSaleStatusLabel('active', t)).toBe('billing.status.active');
    expect(formatSaleStatusLabel(null, t)).toBe('—');
    expect(isSaleCancellable({ status: 'active' })).toBe(true);
    expect(isSaleCancellable({ status: 'cancelled' })).toBe(false);
  });
});

describe('tenantStatusLabel', () => {
  it('maps status colors', () => {
    expect(tenantStatusColor('active')).toBe('green');
    expect(tenantStatusColor('suspended')).toBe('orange');
    expect(tenantStatusColor('deleted')).toBe('red');
    expect(tenantStatusColor('unknown')).toBe('default');
    expect(registerStatusColor('OPEN')).toBe('green');
    expect(registerStatusColor('decommissioned')).toBe('red');
  });
});

describe('buildPitrDateTimeConstraints', () => {
  it('allows all dates when bounds missing', () => {
    const c = buildPitrDateTimeConstraints(null, null);
    expect(c.disabledDate(dayjs())).toBe(false);
    expect(c.disabledTime(null)).toEqual({});
  });

  it('disables outside window and clamps hours on boundary days', () => {
    const earliest = '2026-01-10T10:15:20.000Z';
    const latest = '2026-01-12T18:30:40.000Z';
    const c = buildPitrDateTimeConstraints(earliest, latest);
    expect(c.disabledDate(dayjs('2026-01-09'))).toBe(true);
    expect(c.disabledDate(dayjs('2026-01-11'))).toBe(false);
    const hours = c.disabledTime(dayjs(earliest)).disabledHours?.() ?? [];
    expect(hours).toContain(0);
  });
});
