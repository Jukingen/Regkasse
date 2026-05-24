import type { TenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';

export type HeaderLicenseStatusClass = 'valid' | 'warning' | 'expired';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export function isHeaderLicenseExpired(license: TenantLicenseLabel): boolean {
    if (license.kind === 'expired') {
        return true;
    }
    return license.daysRemaining != null && license.daysRemaining < 0;
}

export function isHeaderLicenseTrial(license: TenantLicenseLabel): boolean {
    return license.kind === 'trial';
}

export function getHeaderLicenseStatusClass(license: TenantLicenseLabel): HeaderLicenseStatusClass {
    const daysRemaining = license.daysRemaining ?? 0;
    if (isHeaderLicenseExpired(license)) {
        return 'expired';
    }
    if (daysRemaining <= 7) {
        return 'warning';
    }
    return 'valid';
}

export function getHeaderLicenseStatusText(license: TenantLicenseLabel, t: TranslateFn): string {
    const daysRemaining = license.daysRemaining ?? 0;
    if (isHeaderLicenseExpired(license)) {
        return t('license.badge.headerShort.expired');
    }
    if (daysRemaining <= 7) {
        return t('license.badge.headerShort.daysRemaining', { days: daysRemaining });
    }
    if (isHeaderLicenseTrial(license)) {
        return t('license.badge.headerShort.trial');
    }
    return t('license.badge.headerShort.licensed');
}
