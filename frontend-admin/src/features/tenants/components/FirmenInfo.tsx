'use client';

import { Alert, Card, Descriptions, Spin, Tag, Typography } from 'antd';

import type { Tenant } from '@/features/tenancy/providers/TenantProvider';
import { formatGermanDateTime, useI18n } from '@/i18n';

export type FirmenInfoProps = {
    /** Active mandant from parent ({@link useTenant} / {@link useCurrentTenant}). */
    tenant: Tenant | null;
    /** Optional loading UI while parent resolves tenant context. */
    loading?: boolean;
    /** Optional error from parent tenant fetch. */
    error?: Error | null;
};

export function FirmenInfo({ tenant, loading = false, error = null }: FirmenInfoProps) {
    const { t } = useI18n();

    if (loading) {
        return (
            <Card title={t('common.tenant.companyInfo')}>
                <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                    <Spin aria-label={t('common.dataList.loadingTip')} />
                </div>
            </Card>
        );
    }

    if (error) {
        return (
            <Alert
                type="error"
                showIcon
                title={t('common.dataList.errorLoadTitle')}
                description={error.message}
            />
        );
    }

    if (!tenant) {
        return null;
    }

    const mandantLabel = `${t('common.tenant.tenant')} (${t('common.tenant.tenantAlt')})`;
    const displayName = tenant.name?.trim() || tenant.slug;
    const licenseLabel = tenant.licenseValid
        ? t('license.phase.labels.active')
        : t('license.phase.labels.noLicense');

    return (
        <Card title={t('common.tenant.companyInfo')}>
            <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
                <Descriptions.Item label={mandantLabel}>{displayName}</Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardName')}>
                    {tenant.name?.trim() || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardSlug')}>
                    <Typography.Text code copyable>
                        {tenant.slug}
                    </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardId')}>
                    <Typography.Text code copyable={{ text: tenant.id }}>
                        {tenant.id}
                    </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.license')}>
                    <Tag color={tenant.licenseValid ? 'green' : 'red'}>{licenseLabel}</Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t('license.mandant.validUntil')}>
                    {formatGermanDateTime(tenant.licenseValidUntilUtc)}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}
