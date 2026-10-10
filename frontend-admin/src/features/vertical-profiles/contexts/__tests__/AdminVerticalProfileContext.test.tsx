import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ADMIN_VERTICAL_PROFILE_STALE_MS } from '@/features/vertical-profiles/contexts/AdminVerticalProfileContext';
import {
  AdminVerticalProfileProvider,
  invalidateAdminVerticalProfileQueries,
  useAdminVerticalProfile,
} from '@/features/vertical-profiles/contexts/AdminVerticalProfileContext';

const harness = vi.hoisted(() => ({
  tenantId: 'tenant-a' as string | null,
  tenantSlug: 'alpha',
  fetch: vi.fn(),
}));

vi.mock('@/features/tenancy/hooks/useTenantContext', () => ({
  useTenantContext: () => ({
    tenantId: harness.tenantId,
    tenantSlug: harness.tenantSlug,
    isImpersonating: false,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { role: 'Manager' } }),
}));

vi.mock('@/lib/axios', () => ({
  customInstance: (...args: unknown[]) => harness.fetch(...args),
}));

function createClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
}

function createWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AdminVerticalProfileProvider>{children}</AdminVerticalProfileProvider>
      </QueryClientProvider>
    );
  };
}

describe('useAdminVerticalProfile', () => {
  beforeEach(() => {
    harness.tenantId = 'tenant-a';
    harness.tenantSlug = 'alpha';
    harness.fetch.mockReset();
  });

  it('returns the ambient profile from the admin vertical-profile response', async () => {
    harness.fetch.mockResolvedValue({
      profileId: 'hair-salon',
      name: 'Hair salon',
      posFeatures: { appointment: true, tables: false, note: 'skip' },
      posLayout: 'appointment',
      overrides: { posFeatures: { appointment: true } },
    });

    const client = createClient();
    const { result } = renderHook(() => useAdminVerticalProfile(), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.profileId).toBe('hair-salon');
    expect(result.current.posFeatures).toEqual({ appointment: true, tables: false });
    expect(result.current.posLayout).toBe('appointment');
    expect(harness.fetch).toHaveBeenCalledWith(
      expect.objectContaining({ url: '/api/admin/vertical-profile', method: 'GET' }),
      undefined
    );

    const cached = client.getQueryCache().findAll();
    expect(cached).toHaveLength(1);
    expect(cached[0]?.queryKey).toEqual(['/api/admin/vertical-profile', 'tenant-a|alpha']);
    expect(cached[0]?.options.staleTime).toBe(ADMIN_VERTICAL_PROFILE_STALE_MS);
  });

  it('refetches when the ambient tenant changes', async () => {
    harness.fetch
      .mockResolvedValueOnce({
        profileId: 'gastronomy',
        posFeatures: { tables: false },
        posLayout: 'standard',
        overrides: {},
      })
      .mockResolvedValueOnce({
        profileId: 'vet',
        posFeatures: { patientRecord: true },
        posLayout: 'standard',
        overrides: { posFeatures: { patientRecord: true } },
      });

    const client = createClient();
    const { result, rerender } = renderHook(() => useAdminVerticalProfile(), {
      wrapper: createWrapper(client),
    });

    await waitFor(() => expect(result.current.profileId).toBe('gastronomy'));
    expect(harness.fetch).toHaveBeenCalledTimes(1);

    harness.tenantId = 'tenant-b';
    harness.tenantSlug = 'beta';
    rerender();

    await waitFor(() => expect(result.current.profileId).toBe('vet'));
    expect(result.current.posFeatures).toEqual({ patientRecord: true });
    expect(harness.fetch).toHaveBeenCalledTimes(2);
    expect(
      client
        .getQueryCache()
        .findAll()
        .map((query) => query.queryKey)
    ).toEqual(
      expect.arrayContaining([
        ['/api/admin/vertical-profile', 'tenant-a|alpha'],
        ['/api/admin/vertical-profile', 'tenant-b|beta'],
      ])
    );
  });

  it('invalidates the scoped sidebar query before its stale time', () => {
    const client = createClient();
    const key = ['/api/admin/vertical-profile', 'tenant-a|alpha'] as const;
    client.setQueryData(key, {
      profileId: 'vet',
      posFeatures: { patientRecord: true },
      posLayout: 'standard',
    });

    invalidateAdminVerticalProfileQueries(client, 'tenant-a');

    expect(client.getQueryCache().find({ queryKey: key })?.state.isInvalidated).toBe(true);
  });
});
