import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';

import {
  type LicensePublicStatusDto,
  tenantLicenseUnifiedQueryKeyFor,
} from '@/api/manual/adminLicense';
import { tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import {
  applyActivatedLicenseToCache,
  beginActivatedLicenseOptimisticUpdate,
  rollbackActivatedLicenseOptimisticUpdate,
  toActiveLicensePublicStatus,
} from '@/features/license/utils/applyActivatedLicenseToCache';
import {
  resolveLicenseValidUntilIso,
  resolveTenantLicenseFromPublicStatus,
  resolveTenantLockFlags,
} from '@/features/license/utils/licenseStatus';
import { currentTenantQueryKey } from '@/features/tenancy/api/getCurrentTenant';
import { mapLicenseLifecycleUiState } from '@/hooks/useLicenseStatus';

const tenantId = 'b0000001-0001-4001-8001-000000000001';
const lockedUntil = '2026-01-01T00:00:00.000Z';
const lockDate = '2026-01-08T00:00:00.000Z';
const newUntil = '2027-08-14T00:00:00.000Z';
const nowMs = new Date('2026-08-14T12:00:00.000Z').getTime();

function lockedStatus(): LicensePublicStatusDto {
  return {
    licenseType: 'Expired',
    validUntil: lockedUntil,
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
    lockDate,
    requiresRenewal: true,
  };
}

describe('applyActivatedLicenseToCache', () => {
  it('replaces locked public status with an active expiry immediately', () => {
    const queryClient = new QueryClient();
    const publicKey = tenantLicenseUnifiedQueryKeyFor(tenantId, 'public');
    queryClient.setQueryData(publicKey, lockedStatus());

    applyActivatedLicenseToCache(
      queryClient,
      { tenantId, validUntilUtc: newUntil, licenseKey: 'REGK-20270814-dev-ABCD1234' },
      nowMs
    );

    const next = queryClient.getQueryData<LicensePublicStatusDto>(publicKey);
    expect(next).toMatchObject({
      validUntil: newUntil,
      isLocked: false,
      isExpired: false,
      isInGracePeriod: false,
      canAccess: true,
      requiresRenewal: false,
      lockDate: null,
    });
    expect(next?.daysRemaining).toBeGreaterThan(0);

    const resolved = resolveTenantLicenseFromPublicStatus(next, nowMs);
    expect(resolved.kind).toBe('active');
    expect(mapLicenseLifecycleUiState({
      ...resolved,
      daysRemainingInGrace: 0,
      isExpired: false,
      isLocked: false,
      lockDate: null,
      message: '',
    })).toBe('Active');
  });

  it('does not use lockDate as the displayed valid-until after extension', () => {
    expect(resolveLicenseValidUntilIso(newUntil)).toBe(newUntil);
    expect(resolveLicenseValidUntilIso(undefined)).toBeNull();
  });

  it('writes admin overview and current-tenant caches', () => {
    const queryClient = new QueryClient();
    queryClient.setQueryData(currentTenantQueryKey, {
      id: tenantId,
      slug: 'dev',
      name: 'Dev',
      licenseValid: false,
      licenseValidUntilUtc: lockedUntil,
    });

    applyActivatedLicenseToCache(
      queryClient,
      { tenantId, validUntilUtc: newUntil, licenseKey: 'REGK-KEY' },
      nowMs
    );

    expect(queryClient.getQueryData(currentTenantQueryKey)).toMatchObject({
      licenseValid: true,
      licenseValidUntilUtc: newUntil,
    });
    expect(queryClient.getQueryData(tenantLicenseQueryKeys.detail(tenantId))).toMatchObject({
      status: { kind: 'active', validUntilUtc: newUntil },
    });
  });

  it('rolls back optimistic license cache when activation fails', async () => {
    const queryClient = new QueryClient();
    const publicKey = tenantLicenseUnifiedQueryKeyFor(tenantId, 'public');
    const locked = lockedStatus();
    queryClient.setQueryData(publicKey, locked);

    const snapshot = await beginActivatedLicenseOptimisticUpdate(queryClient, {
      tenantId,
      validUntilUtc: newUntil,
    });

    expect(queryClient.getQueryData<LicensePublicStatusDto>(publicKey)?.isLocked).toBe(false);

    rollbackActivatedLicenseOptimisticUpdate(queryClient, snapshot);
    expect(queryClient.getQueryData(publicKey)).toEqual(locked);
  });

  it('builds an active DTO even when previous cache is empty', () => {
    const next = toActiveLicensePublicStatus(undefined, { validUntilUtc: newUntil }, nowMs);
    expect(next.isLocked).toBe(false);
    expect(next.canAccess).toBe(true);
    expect(next.validUntil).toBe(newUntil);
  });
});

describe('resolveTenantLockFlags', () => {
  it('clears stale isLocked when resolved kind is active', () => {
    expect(
      resolveTenantLockFlags(
        {
          kind: 'active',
          daysRemaining: 300,
          daysExpired: 0,
          canWrite: true,
          canManageUsers: true,
          canAccess: true,
        },
        { isLocked: true, isExpired: true, isInGracePeriod: false }
      )
    ).toEqual({ isLocked: false, isExpired: false });
  });
});
