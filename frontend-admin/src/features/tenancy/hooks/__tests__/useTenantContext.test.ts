/**
 * useTenantContext – stable useSyncExternalStore snapshots (no render loop after login).
 */
import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      tenantId: '11111111-1111-1111-1111-111111111111',
      tenantSlug: 'dev',
      tenantDisplayName: 'Test Cafe',
    },
  }),
}));

const cafeJwtPayload = btoa(
  JSON.stringify({
    tenant_id: '11111111-1111-1111-1111-111111111111',
    tenant_slug: 'dev',
  })
);

vi.mock('@/features/auth/services/authStorage', () => ({
  authStorage: {
    hasToken: () => true,
    getToken: () => `header.${cafeJwtPayload}.signature`,
  },
}));

describe('useTenantContext', () => {
  it('stabilizes after login without maximum update depth', () => {
    let renderCount = 0;
    const { result } = renderHook(() => {
      renderCount += 1;
      return useTenantContext();
    });

    expect(renderCount).toBeLessThan(5);
    expect(result.current.tenantSlug).toBeTruthy();
    expect(result.current.hasAuthToken).toBe(true);
  });
});
