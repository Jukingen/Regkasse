'use client';

import { Card, Descriptions, Tag, Tooltip, Typography } from 'antd';
import type { CSSProperties } from 'react';

import { useTenantInfo } from '@/features/tenant/hooks/useTenantInfo';
import { formatDate, useI18n } from '@/i18n';

export type TenantInfoCardProps = {
    className?: string;
    style?: CSSProperties;
};

function LicenseStatusTag({
    licenseDisplay,
    daysRemaining,
}: {
    licenseDisplay: 'active' | 'expired' | 'days' | 'unknown';
    daysRemaining: number | null;
}) {
    const { t } = useI18n();

    if (licenseDisplay === 'expired') {
        return <Tag color="red">{t('adminShell.tenant.info.expired')}</Tag>;
    }
    if (licenseDisplay === 'days' && daysRemaining != null) {
        return <Tag color="blue">{t('adminShell.tenant.info.daysRemaining', { days: daysRemaining })}</Tag>;
    }
    if (licenseDisplay === 'active') {
        return <Tag color="green">{t('adminShell.tenant.info.active')}</Tag>;
    }
    return <Typography.Text type="secondary">—</Typography.Text>;
}

export function TenantInfoCard({ className, style }: TenantInfoCardProps) {
    const { t, formatLocale } = useI18n();
    const {
        tenantSlug,
        tenantId,
        tenantName,
        registeredAt,
        licenseDisplay,
        daysRemaining,
        hasAuthToken,
        isLoading,
    } = useTenantInfo();

    if (!hasAuthToken) {
        return null;
    }

    const idPreview = tenantId ? `${tenantId.slice(0, 8)}…` : '—';
    const registeredLabel = registeredAt
        ? formatDate(registeredAt, formatLocale, {
              year: 'numeric',
              month: '2-digit',
              day: '2-digit',
          })
        : '—';

    return (
        <Card
            className={className}
            style={{ marginBottom: 12, ...style }}
            title={t('adminShell.tenant.info.title')}
            size="small"
            loading={isLoading}
        >
            <Descriptions column={1} size="small">
                <Descriptions.Item label={t('adminShell.tenant.info.name')}>
                    {tenantName || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.slug')}>
                    <Typography.Text code copyable>
                        {tenantSlug}
                    </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.id')}>
                    {tenantId ? (
                        <Tooltip title={tenantId}>
                            <Typography.Text code copyable={{ text: tenantId }}>
                                {idPreview}
                            </Typography.Text>
                        </Tooltip>
                    ) : (
                        '—'
                    )}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.license')}>
                    <LicenseStatusTag licenseDisplay={licenseDisplay} daysRemaining={daysRemaining} />
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.registeredAt')}>
                    {registeredLabel}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}
