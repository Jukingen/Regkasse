import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import { getLicenseHoursRemaining } from '@/features/license/utils/licenseStatus';
import {
    clampTenantGraceRemaining,
    TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';
import { formatUserDateTime } from '@/lib/dateFormatter';

export type HeaderLicenseStatusClass = 'valid' | 'warning' | 'expired';

export type HeaderLicenseStatusContext = {
    validUntilUtc?: string | null;
};

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

function isGracePhase(status: ResolvedLicenseStatus): boolean {
    return status.kind === 'grace_write' || status.kind === 'grace_readonly';
}

function isExpiringSoon(status: ResolvedLicenseStatus): boolean {
    return (
        isGracePhase(status)
        || (status.kind === 'active' && status.daysRemaining >= 0 && status.daysRemaining <= 7)
    );
}

function formatExpiryDateTime(validUntilUtc: string | null | undefined): string {
    return formatUserDateTime(validUntilUtc) || '';
}

export { getLicenseHoursRemaining };

function isExpiredStatus(status: ResolvedLicenseStatus): boolean {
    return status.kind === 'lockdown' || status.kind === 'expired' || status.daysRemaining < 0;
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

export function getHeaderLicenseStatusText(
    status: ResolvedLicenseStatus,
    t: TranslateFn,
    context?: HeaderLicenseStatusContext,
): string {
    if (status.kind === 'no_license') {
        return t('license.badge.headerShort.none');
    }

    if (isGracePhase(status)) {
        return t('license.badge.headerShort.expiringSoon');
    }

    if (isExpiredStatus(status)) {
        return t('license.badge.headerShort.expired');
    }

    if (isExpiringSoon(status)) {
        const hoursRemaining = getLicenseHoursRemaining(context?.validUntilUtc);
        if (hoursRemaining !== null && hoursRemaining < 24) {
            return t('license.badge.headerShort.expiringSoonWithHours', { hours: hoursRemaining });
        }

        return t('license.badge.headerShort.expiringSoonWithDays', { days: status.daysRemaining });
    }

    if (status.daysRemaining > 0) {
        return `${t('license.phase.labels.active')} (${t('license.badge.headerShort.daysRemaining', {
            days: status.daysRemaining,
        })})`;
    }

    return t('license.badge.headerShort.licensed');
}

export function getHeaderLicenseTooltipStatusLabel(status: ResolvedLicenseStatus, t: TranslateFn): string {
    if (isGracePhase(status)) {
        return t('license.phase.labels.graceWrite');
    }

    if (status.daysRemaining <= 0 || isExpiredStatus(status)) {
        return t('license.phase.labels.expired');
    }

    return t('license.phase.labels.active');
}

export function hasDetailedHeaderLicenseTooltip(context?: HeaderLicenseStatusContext): boolean {
    return Boolean(formatExpiryDateTime(context?.validUntilUtc));
}

export function getHeaderLicenseTooltip(
    status: ResolvedLicenseStatus,
    t: TranslateFn,
    context?: HeaderLicenseStatusContext,
): string {
    const dateTime = formatExpiryDateTime(context?.validUntilUtc);
    const statusText = getHeaderLicenseStatusText(status, t, context);

    if (!dateTime) {
        return t('license.badge.headerShort.mandantTooltip', { status: statusText });
    }

    const statusLabel = getHeaderLicenseTooltipStatusLabel(status, t);

    if (isGracePhase(status)) {
        const graceRemaining = clampTenantGraceRemaining(
            TENANT_GRACE_PERIOD_DAYS - status.daysExpired,
        );
        return t('license.badge.headerShort.tooltip.ariaSummary', {
            dateTime,
            days: graceRemaining,
            status: statusLabel,
        });
    }

    // Expired / lockdown: never prefer hours-from-ValidUntil (can be a stale future stamp).
    if (!isExpiredStatus(status)) {
        const hoursRemaining = getLicenseHoursRemaining(context?.validUntilUtc);
        if (hoursRemaining !== null && hoursRemaining > 0 && hoursRemaining < 24) {
            return t('license.badge.headerShort.tooltip.ariaSummaryHours', {
                dateTime,
                hours: hoursRemaining,
                status: statusLabel,
            });
        }
    }

    const daysRemaining = Math.max(0, status.daysRemaining);

    return t('license.badge.headerShort.tooltip.ariaSummary', {
        dateTime,
        days: daysRemaining,
        status: statusLabel,
    });
}
