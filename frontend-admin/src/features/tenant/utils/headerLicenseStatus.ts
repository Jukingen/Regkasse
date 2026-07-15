import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';

export type HeaderLicenseStatusClass = 'valid' | 'warning' | 'expired';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

function isGracePhase(status: ResolvedLicenseStatus): boolean {
    return status.kind === 'grace_write' || status.kind === 'grace_readonly';
}

export function getHeaderLicenseStatusClass(status: ResolvedLicenseStatus): HeaderLicenseStatusClass {
    if (status.kind === 'no_license' || status.kind === 'lockdown') {
        return 'expired';
    }

    if (status.kind === 'expired') {
        return 'expired';
    }

    if (isGracePhase(status) || status.daysRemaining <= 7) {
        return 'warning';
    }

    return 'valid';
}

export function getHeaderLicenseStatusText(status: ResolvedLicenseStatus, t: TranslateFn): string {
    if (status.kind === 'no_license') {
        return t('license.badge.headerShort.none');
    }

    if (isGracePhase(status)) {
        return t('license.badge.headerShort.expiringSoon');
    }

    if (status.kind === 'lockdown' || status.kind === 'expired' || status.daysRemaining < 0) {
        return t('license.badge.headerShort.expired');
    }

    if (status.daysRemaining <= 7) {
        return t('license.badge.headerShort.expiringSoon');
    }

    if (status.daysRemaining > 0) {
        return `${t('license.phase.labels.active')} (${t('license.badge.headerShort.daysRemaining', {
            days: status.daysRemaining,
        })})`;
    }

    return t('license.badge.headerShort.licensed');
}

export function getHeaderLicenseTooltip(status: ResolvedLicenseStatus, t: TranslateFn): string {
    const statusText = getHeaderLicenseStatusText(status, t);
    return t('license.badge.headerShort.mandantTooltip', { status: statusText });
}
