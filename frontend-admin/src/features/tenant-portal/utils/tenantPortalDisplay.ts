import type { LicenseLifecycleUiState, LicenseStatusView } from '@/hooks/useLicenseStatus';
import type { AdminTranslationKey } from '@/i18n/translationKey';

export function resolvePortalDisplayName(
  firstName?: string | null,
  lastName?: string | null,
  userName?: string | null
): string {
  const fullName = [firstName, lastName].filter(Boolean).join(' ').trim();
  if (fullName) return fullName;
  return userName?.trim() || 'Manager';
}

export function portalLicenseStatusLabelKey(
  state: LicenseLifecycleUiState | null | undefined
): AdminTranslationKey {
  switch (state) {
    case 'Active':
      return 'tenantPortal.portal.licenseActive';
    case 'Grace':
      return 'tenantPortal.portal.licenseGrace';
    case 'Locked':
    case 'Archived':
      return 'tenantPortal.portal.licenseExpired';
    default:
      return 'tenantPortal.license.unknown';
  }
}

export function portalLicenseStatusColor(
  state: LicenseLifecycleUiState | null | undefined
): string {
  switch (state) {
    case 'Active':
      return 'green';
    case 'Grace':
      return 'orange';
    case 'Locked':
    case 'Archived':
      return 'red';
    default:
      return 'default';
  }
}

export function portalLicenseDaysCopy(
  status: Pick<LicenseStatusView, 'state' | 'daysUntilExpiry' | 'graceDaysRemaining' | 'daysOverdue'> | null
): { key: AdminTranslationKey; days: number } | null {
  if (!status) return null;
  if (status.state === 'Active') {
    return { key: 'tenantPortal.portal.daysRemaining', days: status.daysUntilExpiry };
  }
  if (status.state === 'Grace') {
    return { key: 'tenantPortal.portal.daysRemaining', days: status.graceDaysRemaining };
  }
  return { key: 'tenantPortal.portal.expiredDays', days: status.daysOverdue };
}

export function portalOpenInvoiceCount(list: {
  totalCount?: number;
  activeCount?: number;
  cancelledCount?: number;
} | null): number {
  if (!list) return 0;
  const total = list.totalCount ?? 0;
  const paid = list.activeCount ?? 0;
  const cancelled = list.cancelledCount ?? 0;
  return Math.max(0, total - paid - cancelled);
}

export function isPortalProfileComplete(onboarding: {
  isFullyComplete?: boolean;
  completedCount?: number;
  totalCount?: number;
} | null): boolean {
  if (!onboarding) return false;
  if (onboarding.isFullyComplete) return true;
  const total = Math.max(1, onboarding.totalCount ?? 0);
  return (onboarding.completedCount ?? 0) >= total;
}
