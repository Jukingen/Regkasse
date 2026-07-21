import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
  type TenantListItemForSwitcher,
  tenantNeedsNoAdminWarning,
  useTenantListForSwitcher,
} from '@/features/tenancy/hooks/useTenantListForSwitcher';

const mockUseAuth = vi.fn();
const mockUseGetApiAdminTenants = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/tenancy/api/getApiAdminTenants', () => ({
  useGetApiAdminTenants: (...args: unknown[]) => mockUseGetApiAdminTenants(...args),
}));

function source(overrides: Partial<AdminTenantListItem> = {}): AdminTenantListItem {
  return {
    id: 't1',
    name: 'Café',
    slug: 'cafe',
    status: 'active',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('tenantNeedsNoAdminWarning', () => {
  it('warns only for active tenants without admin email', () => {
    const base: TenantListItemForSwitcher = {
      id: 't1',
      name: 'Café',
      slug: 'cafe',
      status: 'active',
      isActive: true,
      adminEmail: null,
      licenseDaysLeft: null,
      statusIcon: '🟢',
      source: source(),
    };
    expect(tenantNeedsNoAdminWarning(base)).toBe(true);
    expect(tenantNeedsNoAdminWarning({ ...base, adminEmail: 'a@b.at' })).toBe(false);
    expect(tenantNeedsNoAdminWarning({ ...base, isActive: false })).toBe(false);
    expect(tenantNeedsNoAdminWarning({ ...base, status: 'suspended' })).toBe(false);
  });
});

describe('useTenantListForSwitcher', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
    mockUseGetApiAdminTenants.mockReset();
    mockUseGetApiAdminTenants.mockReturnValue({
      data: undefined,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
  });

  it('maps API rows into switcher items when query returns data', () => {
    mockUseAuth.mockReturnValue({ user: { id: 'u1' } });
    mockUseGetApiAdminTenants.mockReturnValue({
      data: [
        source({
          id: 'a',
          name: 'Adler',
          slug: 'adler',
          ownerAdminEmail: ' admin@adler.at ',
          licenseDaysRemaining: 12,
        }),
      ],
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });

    const { result } = renderHook(() => useTenantListForSwitcher());
    expect(result.current.tenantCount).toBe(1);
    expect(result.current.tenants[0]?.name).toBe('Adler');
    expect(result.current.tenants[0]?.adminEmail).toBe('admin@adler.at');
    expect(result.current.tenants[0]?.licenseDaysLeft).toBe(12);
    expect(result.current.tenants[0]?.source.slug).toBe('adler');
  });

  it('passes includeDeleted through to the tenants query', () => {
    mockUseAuth.mockReturnValue({ user: { id: 'u1' } });
    renderHook(() => useTenantListForSwitcher({ includeDeleted: true }));
    expect(mockUseGetApiAdminTenants).toHaveBeenCalledWith(
      { includeDeleted: true },
      expect.objectContaining({ enabled: expect.any(Boolean) })
    );
  });
});
