'use client';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import {
    clampTenantGraceRemaining,
    TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';
import { LicenseExpiryCountdownText } from '@/features/license/components/LicenseExpiryCountdownText';
import {
    getHeaderLicenseTooltipStatusLabel,
    getLicenseHoursRemaining,
    type HeaderLicenseStatusContext,
} from '@/features/tenant/utils/headerLicenseStatus';
import { formatUserDateTime } from '@/lib/dateFormatter';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type HeaderLicenseTooltipContentProps = {
    status: ResolvedLicenseStatus;
    context: HeaderLicenseStatusContext;
    t: TranslateFn;
};

function isGracePhase(status: ResolvedLicenseStatus): boolean {
    return status.kind === 'grace_write' || status.kind === 'grace_readonly';
}

export function HeaderLicenseTooltipContent({ status, context, t }: HeaderLicenseTooltipContentProps) {
    const dateTime = formatUserDateTime(context.validUntilUtc);
    const hoursRemaining = getLicenseHoursRemaining(context.validUntilUtc);
    const showHours =
        !isGracePhase(status) &&
        hoursRemaining !== null &&
        hoursRemaining > 0 &&
        hoursRemaining < 24;
    const daysRemaining = isGracePhase(status)
        ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - status.daysExpired)
        : Math.max(0, status.daysRemaining);
    const statusLabel = getHeaderLicenseTooltipStatusLabel(status, t);

    return (
        <div>
            <div>
                <strong>{t('license.badge.headerShort.tooltip.validUntil')}:</strong> {dateTime}
            </div>
            <div>
                {showHours ? (
                    <>
                        <strong>{t('license.badge.headerShort.tooltip.hoursRemaining')}:</strong>{' '}
                        {hoursRemaining}
                    </>
                ) : (
                    <>
                        <strong>{t('license.badge.headerShort.tooltip.daysRemaining')}:</strong>{' '}
                        {daysRemaining}
                    </>
                )}
            </div>
            <div>
                <strong>{t('license.badge.headerShort.tooltip.status')}:</strong> {statusLabel}
            </div>
            {!isGracePhase(status) ? (
                <LicenseExpiryCountdownText
                    expiresAt={context.validUntilUtc}
                    labelKey="license.badge.headerShort.tooltip.countdown"
                    t={t}
                />
            ) : null}
        </div>
    );
}
