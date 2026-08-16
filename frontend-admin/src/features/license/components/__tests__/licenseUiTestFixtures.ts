import type { LicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import type { LicenseStatusView } from '@/hooks/useLicenseStatus';

export const TENANT_ID = 'tenant-1';
export const ACTIVE_UNTIL = '2027-08-14T00:00:00.000Z';
export const ACTIVE_UNTIL_DISPLAY = '14.08.2027';
export const EXPIRED_UNTIL = '2026-01-01T00:00:00.000Z';
export const EXPIRED_UNTIL_DISPLAY = '01.01.2026';
export const EXTENDED_UNTIL = '2027-12-31T00:00:00.000Z';
export const EXTENDED_UNTIL_DISPLAY = '31.12.2027';

export function interpolateT(table: Record<string, string>) {
  return (key: string, params?: Record<string, string | number>) => {
    let value = table[key] ?? key;
    if (params) {
      for (const [name, replacement] of Object.entries(params)) {
        value = value.replaceAll(`{{${name}}}`, String(replacement));
      }
    }
    return value;
  };
}

export function activeLicenseView(overrides?: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Active',
    graceDaysRemaining: 0,
    daysOverdue: 0,
    daysUntilExpiry: 45,
    licensePlan: 'Standard',
    expiredAt: ACTIVE_UNTIL,
    graceEndedAt: null,
    canWrite: true,
    kind: 'active',
    ...overrides,
  };
}

export function expiredLicenseView(overrides?: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Locked',
    graceDaysRemaining: 0,
    daysOverdue: 12,
    daysUntilExpiry: 0,
    licensePlan: 'Standard',
    expiredAt: EXPIRED_UNTIL,
    graceEndedAt: '2026-01-08T00:00:00.000Z',
    canWrite: false,
    kind: 'lockdown',
    ...overrides,
  };
}

export function graceLicenseView(overrides?: Partial<LicenseStatusView>): LicenseStatusView {
  return {
    state: 'Grace',
    graceDaysRemaining: 5,
    daysOverdue: 2,
    daysUntilExpiry: 0,
    licensePlan: 'Standard',
    expiredAt: EXPIRED_UNTIL,
    graceEndedAt: '2026-01-08T00:00:00.000Z',
    canWrite: true,
    kind: 'grace_write',
    ...overrides,
  };
}

export function extendedLicenseView(): LicenseStatusView {
  return activeLicenseView({
    expiredAt: EXTENDED_UNTIL,
    daysUntilExpiry: 504,
  });
}

export function resolvedLicense(
  kind: ResolvedLicenseStatus['kind'],
  overrides?: Partial<ResolvedLicenseStatus>
): ResolvedLicenseStatus {
  const active = kind === 'active';
  const grace = kind === 'grace_write' || kind === 'grace_readonly';
  return {
    kind,
    daysRemaining: active ? 45 : grace ? 0 : -12,
    daysExpired: active ? 0 : grace ? 2 : 12,
    canWrite: active || kind === 'grace_write',
    canManageUsers: kind !== 'lockdown' && kind !== 'expired' && kind !== 'no_license',
    canAccess: kind !== 'lockdown' && kind !== 'expired' && kind !== 'no_license',
    ...overrides,
  };
}

export function tenantLicenseStatus(
  kind: LicenseStatus['kind'],
  overrides?: Partial<LicenseStatus>
): LicenseStatus {
  const resolved = resolvedLicense(kind);
  return {
    ...resolved,
    daysRemainingInGrace: kind === 'grace_write' ? 5 : 0,
    isExpired: kind !== 'active',
    isLocked: kind === 'lockdown' || kind === 'expired',
    lockDate: null,
    message: '',
    ...overrides,
  };
}
