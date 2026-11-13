import { beforeEach, describe, expect, it, vi } from 'vitest';

import { persistSelectedTenant } from '@/features/tenancy/providers/TenantProvider';

const persistBootstrap = vi.fn();

vi.mock('@/features/auth/services/tenantStorage', () => ({
  tenantStorage: {
    persistBootstrap: (...args: unknown[]) => persistBootstrap(...args),
  },
}));

describe('persistSelectedTenant', () => {
  beforeEach(() => {
    persistBootstrap.mockClear();
  });

  it('persists business tenant id and slug for API bootstrap', () => {
    persistSelectedTenant({
      id: '11111111-1111-1111-1111-111111111111',
      slug: 'cafe-demo',
      name: 'Cafe',
      licenseValid: true,
      licenseValidUntilUtc: null,
    });

    expect(persistBootstrap).toHaveBeenCalledWith({
      tenantId: '11111111-1111-1111-1111-111111111111',
      tenantSlug: 'cafe-demo',
    });
  });

  it('ignores platform admin slug and null', () => {
    persistSelectedTenant(null);
    persistSelectedTenant({
      id: '11111111-1111-1111-1111-111111111111',
      slug: 'admin',
      name: 'Platform',
      licenseValid: true,
      licenseValidUntilUtc: null,
    });
    expect(persistBootstrap).not.toHaveBeenCalled();
  });
});
