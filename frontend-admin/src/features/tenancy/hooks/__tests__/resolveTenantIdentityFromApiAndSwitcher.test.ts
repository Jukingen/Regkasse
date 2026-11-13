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

  const apiOther = {
    id: 'acme-id',
    slug: 'acme',
    name: 'Acme',
    licenseValidUntilUtc: '2026-01-01T00:00:00Z',
  };

  it('prefers switcher/dev identity when API snapshot is a different mandant', () => {
    const result = resolveTenantIdentityFromApiAndSwitcher({
      apiTenant: apiOther,
      resolvedRow: resolvedDev,
      ctxSlug: 'dev',
      ctxName: 'Development',
      jwtTenantId: 'acme-id',
      jwtTenantSlug: 'acme',
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
      apiTenant: apiOther,
      resolvedRow: null,
      ctxSlug: 'admin',
      ctxName: null,
      jwtTenantId: 'acme-id',
      jwtTenantSlug: 'acme',
    });

    expect(result.tenantId).toBe('acme-id');
    expect(result.tenantSlug).toBe('acme');
    expect(result.tenantName).toBe('Acme');
  });
});
