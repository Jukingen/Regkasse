import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';

vi.mock('@/hooks/useTenant', () => ({
  useTenant: () => ({
    tenant: {
      id: 't1',
      slug: 'cafe',
      name: 'Cafe',
      licenseValid: true,
      licenseValidUntilUtc: null,
    },
    isLoading: false,
    setTenant: vi.fn(),
    error: null,
    refresh: vi.fn(),
    tenants: [],
    tenantsLoading: false,
  }),
}));

describe('useTsePageTenant', () => {
  it('exposes tenant id from global useTenant context', () => {
    const { result } = renderHook(() => useTsePageTenant());
    expect(result.current.isReady).toBe(true);
    expect(result.current.tenantId).toBe('t1');
    expect(result.current.tenantName).toBe('Cafe');
  });
});
