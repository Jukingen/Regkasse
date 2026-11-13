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
import { resolveLicenseValidUntilIso, shouldShowSystemLockedAlert } from '@/features/license/utils/licenseStatus';
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
  anyActive: boolean;
  allActive: boolean;
};

/** Maps resolved tenant license → FA lockdown lifecycle state. */
export function mapLicenseLifecycleUiState(license: LicenseStatus): LicenseLifecycleUiState {
  if (license.kind === 'grace_write' || license.kind === 'grace_readonly') {
    return 'Grace';
  }

  // A still-valid (or just-extended) license is Active even if stale isLocked/lockdown flags remain.
  if (
    license.kind === 'active' ||
    (license.daysRemaining >= 0 && license.kind !== 'no_license')
  ) {
    return 'Active';
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

export function shouldShowSystemLockedAlertFromView(view: {
  kind: LicenseStatus['kind'];
  state: LicenseLifecycleUiState;
  daysUntilExpiry: number;
  daysOverdue: number;
}): boolean {
  return shouldShowSystemLockedAlert({
    kind: view.kind,
    daysRemaining: view.state === 'Active' ? view.daysUntilExpiry : -Math.max(0, view.daysOverdue),
    daysExpired: view.daysOverdue,
    state: view.state,
  });
}

function toView(
  license: LicenseStatus,
  expiredAt: string | null,
  licensePlan: string | null,
  anyActive: boolean,
  allActive: boolean
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
    anyActive,
    allActive,
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

    const expiredAt = resolveLicenseValidUntilIso(licenseQuery.data?.validUntil);
    const licensePlan =
      typeof licenseQuery.data?.licenseType === 'string' &&
      licenseQuery.data.licenseType.trim().length > 0
        ? licenseQuery.data.licenseType.trim()
        : null;

    return toView(
      statusQuery.data,
      expiredAt,
      licensePlan,
      Boolean(licenseQuery.data?.anyActive ?? licenseQuery.data?.isValid),
      Boolean(licenseQuery.data?.allActive)
    );
  }, [
    statusQuery.data,
    licenseQuery.data?.validUntil,
    licenseQuery.data?.licenseType,
    licenseQuery.data?.anyActive,
    licenseQuery.data?.allActive,
    licenseQuery.data?.isValid,
  ]);

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
