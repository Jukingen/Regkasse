import type { TenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';

export type HeaderLicenseStatusClass = 'valid' | 'warning' | 'expired';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export function isHeaderLicenseMissing(
    license: TenantLicenseLabel,
    licenseValidUntilUtc?: string | null,
): boolean {
    return license.kind === 'none' || !licenseValidUntilUtc?.trim();
}

export function isHeaderLicenseExpired(license: TenantLicenseLabel): boolean {
    if (license.kind === 'expired') {
        return true;
    }
    return license.daysRemaining != null && license.daysRemaining < 0;
}

export function getHeaderLicenseStatusClass(
    license: TenantLicenseLabel,
    licenseValidUntilUtc?: string | null,
): HeaderLicenseStatusClass {
    if (isHeaderLicenseMissing(license, licenseValidUntilUtc) || isHeaderLicenseExpired(license)) {
        return 'expired';
    }

    const daysRemaining = license.daysRemaining ?? 0;
    if (daysRemaining <= 7) {
        return 'warning';
    }

    return 'valid';
}

export function getHeaderLicenseStatusText(
    license: TenantLicenseLabel,
    t: TranslateFn,
    licenseValidUntilUtc?: string | null,
): string {
    if (isHeaderLicenseMissing(license, licenseValidUntilUtc)) {
        return t('license.badge.headerShort.none');
    }

    if (isHeaderLicenseExpired(license)) {
        return t('license.badge.headerShort.expired');
    }

    const daysRemaining = license.daysRemaining ?? 0;
    if (daysRemaining <= 7) {
        return t('license.badge.headerShort.expiringSoon');
    }

    return t('license.badge.headerShort.licensed');
}

export function getHeaderLicenseTooltip(
    license: TenantLicenseLabel,
    t: TranslateFn,
    licenseValidUntilUtc?: string | null,
): string {
    const status = getHeaderLicenseStatusText(license, t, licenseValidUntilUtc);
    return t('license.badge.headerShort.mandantTooltip', { status });
}
