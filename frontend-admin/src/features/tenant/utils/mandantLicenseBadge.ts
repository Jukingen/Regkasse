import {
    resolveTenantLicenseLabel,
    type TenantLicenseLabel,
} from '@/features/super-admin/utils/tenantLicenseLabel';

export type MandantLicenseBadgeDisplay = {
    label: string;
    color: string;
    tooltip: string;
    daysRemaining: number | null;
};

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

/**
 * Mandant (tenant) SaaS license badge for header and switcher — not server deployment license.
 */
export function getMandantLicenseBadgeDisplay(
    licenseValidUntilUtc: string | null | undefined,
    licenseKey: string | null | undefined,
    t: TranslateFn,
): MandantLicenseBadgeDisplay | null {
    const lic = resolveTenantLicenseLabel(licenseValidUntilUtc, licenseKey);
    return mapTenantLicenseLabelToBadge(lic, t);
}

/**
 * Compact mandant license badge for header tenant switcher rows (always shows a label).
 */
export function getTenantSwitcherLicenseBadgeDisplay(
    licenseValidUntilUtc: string | null | undefined,
    licenseKey: string | null | undefined,
    t: TranslateFn,
    now = Date.now(),
): MandantLicenseBadgeDisplay {
    if (!licenseValidUntilUtc?.trim() && !licenseKey?.trim()) {
        return {
            label: t('license.badge.tenant.none.label'),
            color: 'default',
            tooltip: t('license.badge.tenant.none.tooltip'),
            daysRemaining: null,
        };
    }

    const lic = resolveTenantLicenseLabel(licenseValidUntilUtc, licenseKey, now);
    const baseTooltip = t('license.badge.tenant.baseTooltip');

    if (lic.kind === 'expired' || (lic.daysRemaining != null && lic.daysRemaining <= 0)) {
        return {
            label: t('license.badge.tenant.short.expired'),
            color: 'error',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.expired.tooltip')}`,
            daysRemaining: lic.daysRemaining,
        };
    }

    const days = lic.daysRemaining;
    if (days != null && days <= 7) {
        return {
            label: t('license.badge.tenant.short.days', { days }),
            color: 'warning',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.days.tooltip', { days })}`,
            daysRemaining: days,
        };
    }

    return {
        label: t('license.badge.tenant.short.licensed'),
        color: 'success',
        tooltip: `${baseTooltip} ${t('license.badge.tenant.licensed.tooltip')}`,
        daysRemaining: days,
    };
}

export function mapTenantLicenseLabelToBadge(
    lic: TenantLicenseLabel,
    t: TranslateFn,
): MandantLicenseBadgeDisplay | null {
    if (lic.kind === 'none') {
        return null;
    }

    const days = lic.daysRemaining;
    const baseTooltip = t('license.badge.tenant.baseTooltip');

    if (lic.kind === 'expired') {
        return {
            label: t('license.badge.tenant.expired.label'),
            color: 'red',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.expired.tooltip')}`,
            daysRemaining: days,
        };
    }

    if (lic.kind === 'trial' && days != null && days >= 0) {
        return {
            label: t('license.badge.tenant.trial.label', { days }),
            color: days <= 7 ? 'orange' : 'blue',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.trial.tooltip', { days })}`,
            daysRemaining: days,
        };
    }

    if (lic.kind === 'valid') {
        if (days != null && days >= 0 && days <= 31) {
            return {
                label: t('license.badge.tenant.trial.label', { days }),
                color: days <= 7 ? 'orange' : 'blue',
                tooltip: `${baseTooltip} ${t('license.badge.tenant.trial.tooltip', { days })}`,
                daysRemaining: days,
            };
        }
        return {
            label: t('license.badge.tenant.licensed.label'),
            color: 'green',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.licensed.tooltip')}`,
            daysRemaining: days,
        };
    }

    if (days != null && days >= 0) {
        return {
            label: t('license.badge.tenant.days.label', { days }),
            color: days <= 7 ? 'orange' : 'blue',
            tooltip: `${baseTooltip} ${t('license.badge.tenant.days.tooltip', { days })}`,
            daysRemaining: days,
        };
    }

    return null;
}
