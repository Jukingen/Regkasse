import { describe, expect, it } from 'vitest';

import {
  ADMIN_USERS_FILTER_ALL,
  ADMIN_USERS_FILTER_PLATFORM,
  TENANT_FILTER_ALL_UI,
  buildAdminUsersPageHref,
  readTenantIdFromSearchParams,
  resolveAdminUsersTenantFilterFromSearchParams,
  tenantFilterFromUiValue,
  tenantFilterToUiValue,
} from '../adminUsersPageUrl';

describe('adminUsersPageUrl', () => {
  it('builds href with tenantId query', () => {
    expect(buildAdminUsersPageHref('abc-123')).toBe('/admin/users?tenantId=abc-123');
    expect(buildAdminUsersPageHref()).toBe('/admin/users');
  });

  it('reads tenantId and legacy tenant from search params', () => {
    expect(readTenantIdFromSearchParams(new URLSearchParams('tenantId=x'))).toBe('x');
    expect(readTenantIdFromSearchParams(new URLSearchParams('tenant=y'))).toBe('y');
    expect(readTenantIdFromSearchParams(new URLSearchParams('tenantId=x&tenant=y'))).toBe('x');
  });

  it('resolves platform filter from URL', () => {
    expect(
      resolveAdminUsersTenantFilterFromSearchParams(new URLSearchParams('filter=platform'))
    ).toBe(ADMIN_USERS_FILTER_PLATFORM);
    expect(
      resolveAdminUsersTenantFilterFromSearchParams(new URLSearchParams('tenantId=cafe-id'))
    ).toBe('cafe-id');
  });

  it('maps UI tenant filter value all ↔ internal empty filter', () => {
    expect(tenantFilterToUiValue(ADMIN_USERS_FILTER_ALL)).toBe(TENANT_FILTER_ALL_UI);
    expect(tenantFilterFromUiValue(TENANT_FILTER_ALL_UI)).toBe(ADMIN_USERS_FILTER_ALL);
    expect(tenantFilterToUiValue('cafe-id')).toBe('cafe-id');
  });
});
