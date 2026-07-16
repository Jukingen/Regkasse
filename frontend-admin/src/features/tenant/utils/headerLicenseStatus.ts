import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import { formatUserDateTime } from '@/lib/dateFormatter';

export type HeaderLicenseStatusClass = 'valid' | 'warning' | 'expired';

export type HeaderLicenseStatusContext = {
    validUntilUtc?: string | null;
};

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

const HOUR_MS = 60 * 60 * 1000;

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

export function getLicenseHoursRemaining(validUntilUtc: string | null | undefined, nowMs = Date.now()): number | null {
    if (!validUntilUtc?.trim()) {
        return null;
    }

    const expiresAtMs = new Date(validUntilUtc).getTime();
    if (!Number.isFinite(expiresAtMs)) {
        return null;
    }

    const remainingMs = expiresAtMs - nowMs;
    if (remainingMs <= 0) {
        return 0;
    }

    return Math.ceil(remainingMs / HOUR_MS);
}

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
    const daysRemaining = Math.max(0, status.daysRemaining);

    return t('license.badge.headerShort.tooltip.ariaSummary', {
        dateTime,
        days: daysRemaining,
        status: statusLabel,
    });
}
