import { resolveTenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';

export type MandantLicenseOverviewKind = 'active' | 'expiring_soon' | 'expired' | 'none' | 'trial';

export type MandantLicenseOverviewStatus = {
  kind: MandantLicenseOverviewKind;
  daysRemaining: number | null;
};

const EXPIRING_SOON_DAYS = 7;

export function resolveMandantLicenseOverviewStatus(
  licenseValidUntilUtc: string | null | undefined,
  licenseKey: string | null | undefined,
  licenseDaysRemaining?: number | null,
  now = Date.now()
): MandantLicenseOverviewStatus {
  const hasKey = Boolean(licenseKey?.trim());
  const hasUntil = Boolean(licenseValidUntilUtc?.trim());

  if (!hasUntil && !hasKey) {
    return { kind: 'none', daysRemaining: null };
  }

  const label = resolveTenantLicenseLabel(
    licenseValidUntilUtc,
    licenseKey,
    now,
    licenseDaysRemaining
  );

  if (label.kind === 'expired') {
    return { kind: 'expired', daysRemaining: label.daysRemaining };
  }

  if (!hasKey && hasUntil) {
    return { kind: 'trial', daysRemaining: label.daysRemaining };
  }

  if (!hasUntil) {
    return { kind: 'none', daysRemaining: null };
  }

  const days = label.daysRemaining ?? 0;
  if (days <= EXPIRING_SOON_DAYS) {
    return { kind: 'expiring_soon', daysRemaining: days };
  }

  return { kind: 'active', daysRemaining: days };
}

export function mandantLicenseOverviewKindLabelKey(kind: MandantLicenseOverviewKind): string {
  switch (kind) {
    case 'active':
      return 'license.superAdmin.table.active';
    case 'expiring_soon':
      return 'license.superAdmin.table.expiresSoon';
    case 'expired':
      return 'license.superAdmin.table.expired';
    case 'trial':
      return 'license.superAdmin.table.trial';
    case 'none':
    default:
      return 'license.superAdmin.table.noLicense';
  }
}

export function mandantLicenseOverviewTagColor(kind: MandantLicenseOverviewKind): string {
  switch (kind) {
    case 'active':
      return 'green';
    case 'expiring_soon':
      return 'gold';
    case 'expired':
      return 'red';
    case 'trial':
      return 'blue';
    case 'none':
    default:
      return 'default';
  }
}
