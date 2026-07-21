import { describe, expect, it } from 'vitest';

import { resolveTenantIdentityFromApiAndSwitcher } from '@/features/tenancy/hooks/useCurrentTenantState';

describe('resolveTenantIdentityFromApiAndSwitcher', () => {
  const resolvedDev = {
    id: 'dev-id',
    slug: 'dev',
    name: 'Development',
    licenseValidUntilUtc: '2026-12-31T00:00:00Z',
    licenseKey: 'TEST-DEV',
    licenseDaysRemaining: 100,
  };

  const apiDefault = {
    id: 'default-id',
    slug: 'default',
    name: 'Default',
    licenseValidUntilUtc: '2026-01-01T00:00:00Z',
  };

  it('prefers switcher/dev identity when API snapshot is a different mandant', () => {
    const result = resolveTenantIdentityFromApiAndSwitcher({
      apiTenant: apiDefault,
      resolvedRow: resolvedDev,
      ctxSlug: 'dev',
      ctxName: 'Development',
      jwtTenantId: 'default-id',
      jwtTenantSlug: 'default',
    });

    expect(result).toEqual({
      tenantId: 'dev-id',
      tenantSlug: 'dev',
      tenantName: 'Development',
      licenseValidUntilUtc: '2026-12-31T00:00:00Z',
      licenseKey: 'TEST-DEV',
      licenseDaysRemaining: 100,
    });
  });

  it('uses API license fields when API matches switcher identity', () => {
    const result = resolveTenantIdentityFromApiAndSwitcher({
      apiTenant: {
        id: 'dev-id',
        slug: 'dev',
        name: 'Development',
        licenseValidUntilUtc: '2027-06-01T00:00:00Z',
      },
      resolvedRow: resolvedDev,
      ctxSlug: 'dev',
      ctxName: 'Development',
      jwtTenantId: 'dev-id',
      jwtTenantSlug: 'dev',
    });

    expect(result.tenantId).toBe('dev-id');
    expect(result.licenseValidUntilUtc).toBe('2027-06-01T00:00:00Z');
  });

  it('falls back to API when switcher row is missing', () => {
    const result = resolveTenantIdentityFromApiAndSwitcher({
      apiTenant: apiDefault,
      resolvedRow: null,
      ctxSlug: 'admin',
      ctxName: null,
      jwtTenantId: 'default-id',
      jwtTenantSlug: 'default',
    });

    expect(result.tenantId).toBe('default-id');
    expect(result.tenantSlug).toBe('default');
    expect(result.tenantName).toBe('Default');
  });
});
