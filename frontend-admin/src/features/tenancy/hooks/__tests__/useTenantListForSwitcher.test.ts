import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
  type TenantListItemForSwitcher,
  isDevelopmentTenant,
  isTestTenantSlug,
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

describe('isDevelopmentTenant', () => {
  it('hides platform, test-bar, and test-cafe in every environment', () => {
    expect(isTestTenantSlug('platform')).toBe(true);
    expect(isTestTenantSlug('test-bar')).toBe(true);
    expect(isTestTenantSlug('test_cafe')).toBe(true);
    expect(isDevelopmentTenant('platform', 'development')).toBe(false);
    expect(isDevelopmentTenant('test-bar', 'test')).toBe(false);
    expect(isDevelopmentTenant('test-cafe', 'production')).toBe(false);
  });

  it('in development only allows seeded dev tenants', () => {
    expect(isDevelopmentTenant('dev', 'development')).toBe(true);
    expect(isDevelopmentTenant('development', 'development')).toBe(true);
    expect(isDevelopmentTenant('prod', 'development')).toBe(false);
    expect(isDevelopmentTenant('adler', 'development')).toBe(false);
  });

  it('outside development keeps non-test tenants', () => {
    expect(isDevelopmentTenant('adler', 'test')).toBe(true);
    expect(isDevelopmentTenant('prod', 'production')).toBe(true);
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
        source({
          id: 'platform',
          name: 'Platform',
          slug: 'platform',
        }),
        source({
          id: 'test-bar',
          name: 'Test Bar',
          slug: 'test-bar',
        }),
        source({
          id: 'test-cafe',
          name: 'Test Cafe',
          slug: 'test-cafe',
        }),
        source({
          id: 'inactive-dev',
          name: 'Archived Dev',
          slug: 'dev',
          isActive: false,
          status: 'archived',
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
    expect(result.current.tenants.every((row) => row.slug !== 'platform')).toBe(true);
    expect(result.current.tenants.every((row) => row.slug !== 'test-bar')).toBe(true);
    expect(result.current.tenants.every((row) => row.slug !== 'test-cafe')).toBe(true);
  });

  it('passes includeDeleted through to the tenants query', () => {
    mockUseAuth.mockReturnValue({ user: { id: 'u1' } });
    renderHook(() => useTenantListForSwitcher({ includeDeleted: true }));
    expect(mockUseGetApiAdminTenants).toHaveBeenCalledWith(
      { includeDeleted: true },
      expect.objectContaining({ enabled: expect.any(Boolean) })
    );
  });

  it('keeps inactive development tenants when includeDeleted is true', () => {
    mockUseAuth.mockReturnValue({ user: { id: 'u1' } });
    mockUseGetApiAdminTenants.mockReturnValue({
      data: [
        source({
          id: 'inactive-dev',
          name: 'Archived Dev',
          slug: 'dev',
          isActive: false,
          status: 'archived',
        }),
        source({
          id: 'test-bar',
          name: 'Test Bar',
          slug: 'test-bar',
          isActive: false,
          status: 'archived',
        }),
      ],
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });

    const { result } = renderHook(() => useTenantListForSwitcher({ includeDeleted: true }));
    expect(result.current.tenants.map((row) => row.slug)).toEqual(['dev']);
  });
});
