import { describe, expect, it } from 'vitest';

import { buildLimitDashboardCsvRows } from '../exportLimitDashboardCsv';
import { limitDashboardDetailHref } from '../limitDashboardShared';
import {
  buildLimitDashboardHref,
  parseLimitDashboardSearch,
} from '../limitDashboardUrl';

describe('limitDashboard URL', () => {
  it('parses tenant and register query params', () => {
    const parsed = parseLimitDashboardSearch(
      new URLSearchParams('tenantId=t-1&registerId=r-9')
    );
    expect(parsed).toEqual({ allTenants: false, tenantId: 't-1', registerId: 'r-9' });
  });

  it('drops tenant and register when allTenants is set', () => {
    const parsed = parseLimitDashboardSearch(
      new URLSearchParams('allTenants=1&tenantId=t-1&registerId=r-9')
    );
    expect(parsed.allTenants).toBe(true);
    expect(parsed.tenantId).toBeUndefined();
    expect(parsed.registerId).toBeUndefined();
  });

  it('builds a shareable dashboard href', () => {
    expect(buildLimitDashboardHref({ allTenants: true })).toBe(
      '/admin/limits/dashboard?allTenants=1'
    );
    expect(buildLimitDashboardHref({ tenantId: 't-1', registerId: 'r-9' })).toBe(
      '/admin/limits/dashboard?tenantId=t-1&registerId=r-9'
    );
  });
});

describe('limitDashboardDetailHref', () => {
  it('opens tenant limits tab for SuperAdmin', () => {
    expect(limitDashboardDetailHref('maxProductsPerTenant', 'tenant-1', true)).toBe(
      '/admin/tenants/tenant-1?tab=limits'
    );
  });

  it('opens operational pages for Mandanten-Admin', () => {
    expect(limitDashboardDetailHref('maxProductsPerTenant', 'tenant-1', false)).toBe('/products');
    expect(limitDashboardDetailHref('maxUsersPerTenant', 'tenant-1', false)).toBe('/admin/users');
    expect(limitDashboardDetailHref('maxActiveRegistersPerUser', 'tenant-1', false)).toBe(
      '/kassenverwaltung'
    );
  });
});

describe('buildLimitDashboardCsvRows', () => {
  it('includes header and usage columns', () => {
    const csv = buildLimitDashboardCsvRows(
      [
        {
          tenantId: 't1',
          tenantName: 'Cafe',
          key: 'maxProductsPerTenant',
          displayName: 'Products',
          description: '',
          current: 8,
          limit: 10,
          percentage: 80,
          status: 'Warning',
          trend: 'Increasing',
          changeCount: 2,
          changeUnit: 'products',
        },
      ],
      {
        tenant: 'Tenant',
        key: 'Key',
        name: 'Name',
        current: 'Current',
        limit: 'Limit',
        percentage: 'Percent',
        status: 'Status',
        trend: 'Trend',
        changeCount: 'Change',
        changeUnit: 'Unit',
      },
      (key, fallback) => fallback || key
    );

    expect(csv).toContain('Cafe');
    expect(csv).toContain('maxProductsPerTenant');
    expect(csv).toContain('80');
  });
});
