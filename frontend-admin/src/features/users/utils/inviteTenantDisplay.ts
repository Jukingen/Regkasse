import {
  type AdminTenantListItem,
  buildTenantPortalUrl,
} from '@/features/super-admin/api/adminTenants';
import { resolveTenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';
import { getTenantSelectorStatus } from '@/features/super-admin/utils/tenantSelectorLabel';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type InviteTenantContextFields = Pick<
  AdminTenantListItem,
  'name' | 'slug' | 'licenseValidUntilUtc' | 'licenseKey' | 'ownerAdminEmail' | 'isDemoPreset'
>;

/** Host only for dropdown labels (e.g. dev.regkasse.at). */
export function buildTenantPortalHost(slug: string): string {
  return buildTenantPortalUrl(slug).replace(/^https?:\/\//, '');
}

/** Short license suffix for invite tenant pickers (e.g. "TESTVERSION (20 Tage)" or "Keine Lizenz"). */
export function formatInviteTenantLicenseShort(
  tenant: Pick<AdminTenantListItem, 'licenseValidUntilUtc' | 'licenseKey'>,
  t: TranslateFn
): string {
  const lic = resolveTenantLicenseLabel(tenant.licenseValidUntilUtc, tenant.licenseKey);
  if (lic.kind === 'none') {
    return t('users.create.licenseNone');
  }
  if (lic.kind === 'expired') {
    return t('license.badge.expired.label');
  }
  if (lic.kind === 'trial' && lic.daysRemaining != null) {
    return t('license.badge.trial.label', { days: lic.daysRemaining });
  }
  if (lic.kind === 'valid' && lic.daysRemaining != null && lic.daysRemaining <= 31) {
    return t('license.badge.trial.label', { days: lic.daysRemaining });
  }
  return t('license.badge.licensed.label');
}

/** Checkbox / select row: "Test Cafe (cafe) — TESTVERSION (20 Tage) — Kein Admin" */
export function buildInviteTenantPickerLabel(tenant: AdminTenantListItem, t: TranslateFn): string {
  const license = formatInviteTenantLicenseShort(tenant, t);
  const status = getTenantSelectorStatus(tenant, t);
  const statusShort =
    status.kind === 'noAdmin'
      ? t('superadmin.selector.noAdmin')
      : status.kind === 'demo'
        ? t('superadmin.selector.demoTenant')
        : '';
  const base = t('users.create.tenantOption', { name: tenant.name, slug: tenant.slug });
  const parts = [base, license];
  if (statusShort) {
    parts.push(statusShort);
  }
  return parts.join(' — ');
}
