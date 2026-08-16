import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  type LicensePublicStatusDto,
  tenantLicenseUnifiedQueryKeyFor,
} from '@/api/manual/adminLicense';
import { useExtendTenantLicense } from '@/features/license/hooks/useExtendTenantLicense';
import {
  resolveTenantLicenseFromPublicStatus,
  shouldShowSystemLockedAlert,
} from '@/features/license/utils/licenseStatus';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { mapLicenseLifecycleUiState } from '@/hooks/useLicenseStatus';

import {
  EXTENDED_UNTIL,
  EXTENDED_UNTIL_DISPLAY,
  TENANT_ID,
} from '@/features/license/components/__tests__/licenseUiTestFixtures';

const activateUnifiedLicense = vi.hoisted(() => vi.fn());

vi.mock('@/features/license/api/activateUnifiedLicense', () => ({
  activateUnifiedLicense: (...args: unknown[]) => activateUnifiedLicense(...args),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ error: vi.fn(), success: vi.fn() }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({ t: (key: string) => key }),
}));

function lockedStatus(): LicensePublicStatusDto {
  return {
    licenseType: 'Expired',
    validUntil: '2026-01-01T00:00:00.000Z',
    daysRemaining: -40,
    features: ['pos'],
    isExpired: true,
    isValid: false,
    mode: 'Production',
    canAccess: false,
    canTransact: false,
    isInGracePeriod: false,
    isLocked: true,
    daysOverdue: 40,
    gracePeriodRemaining: 0,
    lockDate: '2026-01-08T00:00:00.000Z',
    requiresRenewal: true,
  };
}

describe('useExtendTenantLicense cache updates', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    activateUnifiedLicense.mockReset();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
  });

  it('applies the new expiry, clears lockdown, and invalidates React Query caches', async () => {
    const publicKey = tenantLicenseUnifiedQueryKeyFor(TENANT_ID, 'public');
    queryClient.setQueryData(publicKey, lockedStatus());
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    activateUnifiedLicense.mockResolvedValue({
      success: true,
      licenseKey: 'REGK-20271231-dev-ABCD1234',
      validUntilUtc: EXTENDED_UNTIL,
      status: 'Licensed',
      message: 'ok',
    });

    const wrapper = ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);

    const { result } = renderHook(() => useExtendTenantLicense(TENANT_ID), { wrapper });

    await act(async () => {
      await result.current.mutateAsync({
        licenseKey: 'REGK-20271231-dev-ABCD1234',
        expectedValidUntilUtc: EXTENDED_UNTIL,
      });
    });

    const next = queryClient.getQueryData<LicensePublicStatusDto>(publicKey);
    expect(next).toMatchObject({
      validUntil: EXTENDED_UNTIL,
      isLocked: false,
      isExpired: false,
      isInGracePeriod: false,
      canAccess: true,
      requiresRenewal: false,
    });
    expect(formatLicenseValidUntil(next?.validUntil)).toBe(EXTENDED_UNTIL_DISPLAY);

    const resolved = resolveTenantLicenseFromPublicStatus(next);
    expect(resolved.kind).toBe('active');
    expect(
      mapLicenseLifecycleUiState({
        ...resolved,
        daysRemainingInGrace: 0,
        isExpired: false,
        isLocked: false,
        lockDate: null,
        message: '',
      })
    ).toBe('Active');
    expect(
      shouldShowSystemLockedAlert({
        kind: resolved.kind,
        daysRemaining: resolved.daysRemaining,
        daysExpired: resolved.daysExpired,
        state: 'Active',
      })
    ).toBe(false);

    expect(invalidateSpy).toHaveBeenCalled();
    expect(
      invalidateSpy.mock.calls.some((call) => call[0]?.queryKey?.[0] === 'license')
    ).toBe(true);
    expect(
      invalidateSpy.mock.calls.some((call) => call[0]?.queryKey?.[0] === '/api/license/status')
    ).toBe(true);
  });
});
