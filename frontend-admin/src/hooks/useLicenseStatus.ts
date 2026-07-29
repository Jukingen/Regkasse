'use client';

import { useMemo } from 'react';

import type { TenantLicenseHistoryItem } from '@/features/license/api/tenantLicense';
import {
  TENANT_ARCHIVE_AFTER_DAYS,
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
} from '@/features/license/constants/licenseGracePeriod';
import { useLicenseHistory } from '@/features/license/hooks/useLicenseHistory';
import {
  type LicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useTenantLicense } from '@/hooks/useTenantLicense';

/** Mandant lifecycle states for FA lockdown UI (mirrors backend `LicenseLifecycleState`). */
export type LicenseLifecycleUiState = 'Active' | 'Grace' | 'Locked' | 'Archived';

export type LicenseStatusView = {
  state: LicenseLifecycleUiState;
  graceDaysRemaining: number;
  daysOverdue: number;
  /** Calendar days remaining while Active (0 when expired / grace / locked). */
  daysUntilExpiry: number;
  /** Display plan / license type when known. */
  licensePlan: string | null;
  /** ISO expiry / valid-until when known. */
  expiredAt: string | null;
  /** ISO date when grace ended / POS lock started. */
  graceEndedAt: string | null;
  canWrite: boolean;
  /** Underlying resolved license kind. */
  kind: LicenseStatus['kind'];
};

/** Maps resolved tenant license → FA lockdown lifecycle state. */
export function mapLicenseLifecycleUiState(license: LicenseStatus): LicenseLifecycleUiState {
  if (license.kind === 'active') {
    return 'Active';
  }

  if (license.kind === 'grace_write' || license.kind === 'grace_readonly') {
    return 'Grace';
  }

  const daysOverdue = Math.max(0, license.daysExpired);
  if (daysOverdue > TENANT_ARCHIVE_AFTER_DAYS) {
    return 'Archived';
  }

  if (
    license.isLocked ||
    license.kind === 'lockdown' ||
    license.kind === 'expired' ||
    license.kind === 'no_license'
  ) {
    return 'Locked';
  }

  return 'Active';
}

function toView(
  license: LicenseStatus,
  expiredAt: string | null,
  licensePlan: string | null
): LicenseStatusView {
  const state = mapLicenseLifecycleUiState(license);
  const daysOverdue = Math.max(0, license.daysExpired);
  const graceDaysRemaining =
    state === 'Grace'
      ? clampTenantGraceRemaining(
          license.daysRemainingInGrace > 0
            ? license.daysRemainingInGrace
            : TENANT_GRACE_PERIOD_DAYS - daysOverdue
        )
      : 0;
  const daysUntilExpiry = state === 'Active' ? Math.max(0, license.daysRemaining) : 0;

  return {
    state,
    graceDaysRemaining,
    daysOverdue,
    daysUntilExpiry,
    licensePlan,
    expiredAt,
    graceEndedAt: license.lockDate,
    canWrite: license.canWrite,
    kind: license.kind,
  };
}

/**
 * FA lockdown banner / renewal modal / status dashboard view of the current mandant license.
 * Builds on {@link useTenantLicenseStatus} and maps kinds → Active / Grace / Locked / Archived.
 */
export function useLicenseStatus() {
  const statusQuery = useTenantLicenseStatus();
  const licenseQuery = useTenantLicense();
  const historyQuery = useLicenseHistory();

  const status = useMemo(() => {
    if (!statusQuery.data) {
      return null;
    }

    const expiredAt =
      licenseQuery.data?.validUntil ??
      (typeof statusQuery.data.lockDate === 'string' ? statusQuery.data.lockDate : null);
    const licensePlan =
      typeof licenseQuery.data?.licenseType === 'string' &&
      licenseQuery.data.licenseType.trim().length > 0
        ? licenseQuery.data.licenseType.trim()
        : null;

    return toView(statusQuery.data, expiredAt, licensePlan);
  }, [statusQuery.data, licenseQuery.data?.validUntil, licenseQuery.data?.licenseType]);

  const history: TenantLicenseHistoryItem[] = historyQuery.data ?? [];

  return {
    status,
    history,
    isLoading: statusQuery.isLoading || licenseQuery.isLoading,
    isHistoryLoading: historyQuery.isLoading,
    isFetching: statusQuery.isFetching || licenseQuery.isFetching || historyQuery.isFetching,
    refetch: async () => {
      await Promise.all([
        statusQuery.refetch(),
        licenseQuery.refetch(),
        historyQuery.refetch(),
      ]);
    },
  };
}
