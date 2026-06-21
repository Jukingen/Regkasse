'use client';

import React from 'react';
import { Alert, Statistic, Typography } from 'antd';
import dayjs from 'dayjs';
import Link from 'next/link';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { formatDate } from '@/i18n';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

export function LicenseExpiryWidget({ title, dragHandleProps }: Props) {
    const { t, formatLocale } = useI18n();
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.SETTINGS_MANAGE });
    const { licenseValidUntilUtc, licenseDaysRemaining } = useCurrentTenant();

    if (!isAuthorized) {
        return null;
    }

    const days =
        licenseDaysRemaining ??
        (licenseValidUntilUtc
            ? Math.max(0, dayjs(licenseValidUntilUtc).diff(dayjs(), 'day'))
            : null);

    const severity =
        days == null ? 'info' : days <= 7 ? 'error' : days <= 30 ? 'warning' : 'success';

    return (
        <WidgetShell title={title} dragHandleProps={dragHandleProps}>
            {licenseValidUntilUtc ? (
                <>
                    <Statistic
                        title={t('dashboard.widgets.licenseExpiry.daysRemaining')}
                        value={days ?? '—'}
                        styles={{ content: { 
                            color:
                                severity === 'error'
                                    ? '#cf1322'
                                    : severity === 'warning'
                                      ? '#d48806'
                                      : '#3f8600',
                         } }}
                    />
                    <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                        {t('dashboard.widgets.licenseExpiry.validUntil', {
                            date: formatDate(licenseValidUntilUtc, formatLocale),
                        })}
                    </Typography.Paragraph>
                    {days != null && days <= 30 ? (
                        <Alert
                            style={{ marginTop: 12 }}
                            type={severity === 'error' ? 'error' : 'warning'}
                            showIcon
                            title={
                                days <= 7
                                    ? t('dashboard.widgets.licenseExpiry.expiresSoon7')
                                    : t('dashboard.widgets.licenseExpiry.expiresSoon30')
                            }
                        />
                    ) : null}
                </>
            ) : (
                <Alert
                    type="info"
                    showIcon
                    title={t('dashboard.widgets.licenseExpiry.noExpiryDate')}
                    description={
                        <Link href="/settings">{t('dashboard.widgets.licenseExpiry.openSettings')}</Link>
                    }
                />
            )}
        </WidgetShell>
    );
}
