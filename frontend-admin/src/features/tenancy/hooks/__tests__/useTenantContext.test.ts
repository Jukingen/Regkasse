/**
 * useTenantContext – stable useSyncExternalStore snapshots (no render loop after login).
 */
import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';

const { mockIsImpersonating } = vi.hoisted(() => ({
  mockIsImpersonating: vi.fn(() => false),
}));

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
    isImpersonating: () => mockIsImpersonating(),
  },
}));

describe('useTenantContext', () => {
  beforeEach(() => {
    mockIsImpersonating.mockReset();
    mockIsImpersonating.mockReturnValue(false);
  });

  it('stabilizes after login without maximum update depth', () => {
    let renderCount = 0;
    const { result } = renderHook(() => {
      renderCount += 1;
      return useTenantContext();
    });

    expect(renderCount).toBeLessThan(5);
    expect(result.current.tenantSlug).toBeTruthy();
    expect(result.current.hasAuthToken).toBe(true);
    expect(result.current.isImpersonating).toBe(false);
  });

  it('surfaces impersonation from authStorage for impersonated sessions', () => {
    mockIsImpersonating.mockReturnValue(true);
    const { result } = renderHook(() => useTenantContext());

    expect(result.current.hasAuthToken).toBe(true);
    expect(result.current.isImpersonating).toBe(true);
    expect(result.current.tenantId).toBe('11111111-1111-1111-1111-111111111111');
  });
});
