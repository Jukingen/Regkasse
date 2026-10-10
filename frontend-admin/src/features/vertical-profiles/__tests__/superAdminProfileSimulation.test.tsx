import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, renderHook, waitFor } from '@testing-library/react';
import type { MenuProps } from 'antd';
import type { ReactNode } from 'react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { SuperAdminProfileSimulationSelect } from '@/features/vertical-profiles/components/SuperAdminProfileSimulationSelect';
import {
  AdminVerticalProfileProvider,
  useAdminVerticalProfile,
} from '@/features/vertical-profiles/contexts/AdminVerticalProfileContext';
import {
  SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY,
  writeSuperAdminProfileSimulation,
} from '@/features/vertical-profiles/superAdminProfileSimulation';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import { toVerticalProfileMenuScope } from '@/shared/sidebarVerticalProfile';

const harness = vi.hoisted(() => ({
  role: 'SuperAdmin' as string,
  isImpersonating: false,
  fetch: vi.fn(),
}));

vi.mock('@/features/tenancy/hooks/useTenantContext', () => ({
  useTenantContext: () => ({
    tenantId: 'tenant-a',
    tenantSlug: 'alpha',
    isImpersonating: harness.isImpersonating,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { role: harness.role } }),
}));

vi.mock('@/lib/axios', () => ({
  customInstance: (...args: unknown[]) => harness.fetch(...args),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({ t: (key: string) => key }),
}));

const GATED = [
  '/admin/rooms',
  '/admin/tickets/redemptions',
  '/modifier-groups',
  '/tables',
  '/admin/kitchen',
] as const;

function leafKeys(scope: ReturnType<typeof toVerticalProfileMenuScope>): string[] {
  const { menuItems } = buildAdminSidebarMenuItems({
    t: (key) => key,
    verificationNavLabel: 'verify',
    verticalProfile: scope,
  });
  const keys: string[] = [];
  const walk = (items: MenuProps['items'] | undefined) => {
    for (const item of items ?? []) {
      if (!item || typeof item !== 'object' || ('type' in item && item.type === 'divider'))
        continue;
      const node = item as { key?: string; children?: MenuProps['items'] };
      if (node.children?.length) {
        walk(node.children);
        continue;
      }
      if (typeof node.key === 'string') keys.push(node.key);
    }
  };
  walk(menuItems);
  return keys;
}

function wrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AdminVerticalProfileProvider>{children}</AdminVerticalProfileProvider>
      </QueryClientProvider>
    );
  };
}

describe('Super Admin profile simulation', () => {
  beforeEach(() => {
    harness.role = 'SuperAdmin';
    harness.isImpersonating = false;
    harness.fetch.mockReset();
    window.localStorage.removeItem(SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY);
    writeSuperAdminProfileSimulation('all');
    harness.fetch.mockResolvedValue({
      profileId: 'gastronomy',
      posFeatures: { kitchenDisplay: true, tables: false },
      posLayout: 'standard',
      overrides: {},
    });
  });

  it('shows every profile menu by default when Super Admin is not impersonating', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useAdminVerticalProfile(), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.viewAllProfiles).toBe(true);
    expect(result.current.profileId).toBe('gastronomy');

    const keys = leafKeys(toVerticalProfileMenuScope(result.current));
    for (const gated of GATED) {
      expect(keys, gated).toContain(gated);
    }
    expect(keys).toContain('/admin/users');
  });

  it('hides other profile items when Super Admin simulates vet', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useAdminVerticalProfile(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      writeSuperAdminProfileSimulation('vet');
    });

    await waitFor(() => expect(result.current.profileId).toBe('vet'));
    expect(result.current.viewAllProfiles).toBe(false);
    expect(result.current.posFeatures.patientRecord).toBe(true);
    expect(result.current.posFeatures.kitchenDisplay).toBe(false);
    expect(window.localStorage.getItem(SUPER_ADMIN_PROFILE_SIMULATION_STORAGE_KEY)).toBe('vet');

    const keys = leafKeys(toVerticalProfileMenuScope(result.current));
    for (const gated of GATED) {
      expect(keys, gated).not.toContain(gated);
    }
    expect(keys).toContain('/dashboard');
  });

  it('keeps the tenant profile filter while impersonating', async () => {
    harness.isImpersonating = true;
    writeSuperAdminProfileSimulation('all');
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useAdminVerticalProfile(), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.viewAllProfiles).toBe(false);
    expect(result.current.profileId).toBe('gastronomy');

    const keys = leafKeys(toVerticalProfileMenuScope(result.current));
    expect(keys).not.toContain('/admin/rooms');
    expect(keys).toContain('/admin/kitchen');
  });
});

describe('SuperAdminProfileSimulationSelect', () => {
  beforeEach(() => {
    harness.role = 'SuperAdmin';
    harness.isImpersonating = false;
  });

  it('is available to Super Admin and hidden while impersonating or for other roles', () => {
    const { getByTestId, unmount } = render(<SuperAdminProfileSimulationSelect />);
    expect(getByTestId('super-admin-profile-simulation')).toBeInTheDocument();
    unmount();

    harness.isImpersonating = true;
    const impersonating = render(<SuperAdminProfileSimulationSelect />);
    expect(impersonating.queryByTestId('super-admin-profile-simulation')).not.toBeInTheDocument();
    impersonating.unmount();

    harness.role = 'Manager';
    harness.isImpersonating = false;
    const manager = render(<SuperAdminProfileSimulationSelect />);
    expect(manager.queryByTestId('super-admin-profile-simulation')).not.toBeInTheDocument();
  });
});
