import { describe, expect, it } from 'vitest';

import {
  TENANT_ARCHIVE_AFTER_DAYS,
  TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';
import type { LicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { mapLicenseLifecycleUiState, shouldShowSystemLockedAlertFromView } from '@/hooks/useLicenseStatus';

function base(partial: Partial<LicenseStatus>): LicenseStatus {
  return {
    kind: 'active',
    daysRemaining: 30,
    daysExpired: 0,
    daysRemainingInGrace: 0,
    canWrite: true,
    canManageUsers: true,
    canAccess: true,
    isExpired: false,
    isLocked: false,
    lockDate: null,
    message: '',
    ...partial,
  };
}

describe('mapLicenseLifecycleUiState', () => {
  it('maps active and grace', () => {
    expect(mapLicenseLifecycleUiState(base({ kind: 'active' }))).toBe('Active');
    expect(
      mapLicenseLifecycleUiState(
        base({
          kind: 'grace_write',
          daysExpired: 2,
          daysRemainingInGrace: TENANT_GRACE_PERIOD_DAYS - 2,
          isExpired: true,
        })
      )
    ).toBe('Grace');
  });

  it('maps active even when a stale isLocked flag is present', () => {
    expect(
      mapLicenseLifecycleUiState(
        base({
          kind: 'active',
          daysRemaining: 200,
          isLocked: true,
          isExpired: true,
        })
      )
    ).toBe('Active');
  });

  it('maps locked and archived by overdue days', () => {
    expect(
      mapLicenseLifecycleUiState(
        base({
          kind: 'lockdown',
          daysExpired: 10,
          daysRemaining: -10,
          isLocked: true,
          isExpired: true,
          canWrite: false,
        })
      )
    ).toBe('Locked');

    expect(
      mapLicenseLifecycleUiState(
        base({
          kind: 'lockdown',
          daysExpired: TENANT_ARCHIVE_AFTER_DAYS + 1,
          daysRemaining: -(TENANT_ARCHIVE_AFTER_DAYS + 1),
          isLocked: true,
          isExpired: true,
          canWrite: false,
        })
      )
    ).toBe('Archived');
  });

  it('drops Locked after extension when remaining days are positive', () => {
    expect(
      mapLicenseLifecycleUiState(
        base({
          kind: 'lockdown',
          daysRemaining: 200,
          daysExpired: 0,
          isLocked: true,
          isExpired: true,
        })
      )
    ).toBe('Active');
  });

  it('hides System gesperrt after extension via the dashboard alert helper', () => {
    expect(
      shouldShowSystemLockedAlertFromView({
        kind: 'active',
        state: 'Active',
        daysUntilExpiry: 200,
        daysOverdue: 0,
      })
    ).toBe(false);

    expect(
      shouldShowSystemLockedAlertFromView({
        kind: 'lockdown',
        state: 'Locked',
        daysUntilExpiry: 0,
        daysOverdue: 12,
      })
    ).toBe(true);
  });
});
