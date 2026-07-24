import { describe, expect, it } from 'vitest';

import {
  hasSuperAdminActiveTenantContext,
  isPathAllowedWithoutTenant,
} from '@/features/tenancy/hooks/useSuperAdminTenantMode';

describe('isPathAllowedWithoutTenant', () => {
  it('allows platform admin routes without mandant', () => {
    expect(isPathAllowedWithoutTenant('/admin')).toBe(true);
    expect(isPathAllowedWithoutTenant('/admin/tenants')).toBe(true);
    expect(isPathAllowedWithoutTenant('/admin/license')).toBe(true);
    expect(isPathAllowedWithoutTenant('/admin/system/time-sync')).toBe(true);
    expect(isPathAllowedWithoutTenant('/admin/digital')).toBe(true);
  });

  it('blocks mandant-scoped routes', () => {
    expect(isPathAllowedWithoutTenant('/admin/users')).toBe(true);
    expect(isPathAllowedWithoutTenant('/admin/tse/failover')).toBe(false);
    expect(isPathAllowedWithoutTenant('/users')).toBe(false);
    expect(isPathAllowedWithoutTenant('/settings')).toBe(false);
    expect(isPathAllowedWithoutTenant('/products')).toBe(false);
    expect(isPathAllowedWithoutTenant('/dashboard')).toBe(false);
  });
});

describe('hasSuperAdminActiveTenantContext', () => {
  const base = {
    isImpersonating: false,
    isDevTenantOverride: false,
    isPlatformAdminHost: true,
    tenantSlug: 'admin',
    jwtTenantSlug: null as string | null,
    tenantId: null as string | null,
  };

  it('is false on platform host without mandant', () => {
    expect(hasSuperAdminActiveTenantContext(base)).toBe(false);
  });

  it('is true with impersonation', () => {
    expect(hasSuperAdminActiveTenantContext({ ...base, isImpersonating: true })).toBe(true);
  });

  it('is true with dev tenant override', () => {
    expect(hasSuperAdminActiveTenantContext({ ...base, isDevTenantOverride: true })).toBe(true);
  });

  it('is true when JWT is bound to a business tenant', () => {
    expect(
      hasSuperAdminActiveTenantContext({
        ...base,
        jwtTenantSlug: 'cafe-demo',
        tenantId: '11111111-1111-1111-1111-111111111111',
      })
    ).toBe(true);
  });

  it('is true on non-platform host with business slug', () => {
    expect(
      hasSuperAdminActiveTenantContext({
        ...base,
        isPlatformAdminHost: false,
        tenantSlug: 'cafe-demo',
      })
    ).toBe(true);
  });
});
