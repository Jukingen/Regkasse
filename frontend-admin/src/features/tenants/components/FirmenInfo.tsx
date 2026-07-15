'use client';

import { Alert, Card, Descriptions, Spin, Tag, Tooltip, Typography } from 'antd';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusTagColor,
} from '@/features/license/utils/licenseStatus';
import { useTenantInfo } from '@/features/tenant/hooks/useTenantInfo';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { formatTenantDisplay } from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { formatDate, useI18n } from '@/i18n';

export type FirmenInfoTenant = {
    id: string;
    name: string | null;
    slug: string;
    createdAt: string | null;
    licenseStatus: ResolvedLicenseStatus;
};

export type FirmenInfoProps = {
    /** When omitted, resolves from header / switcher / JWT tenant context. */
    tenant?: FirmenInfoTenant | null;
    loading?: boolean;
};

function LicenseStatusTag({ licenseStatus }: { licenseStatus: ResolvedLicenseStatus }) {
    const { t } = useI18n();
    const tooltip = getLicenseStatusDayText(licenseStatus, t);
    const message = getLicenseStatusMessage(licenseStatus, 'tenant', t);

    return (
        <Tooltip title={tooltip ? `${message} ${tooltip}` : message}>
            <Tag color={getLicenseStatusTagColor(licenseStatus.kind)}>
                {getLicenseStatusLabel(licenseStatus.kind, t)}
            </Tag>
        </Tooltip>
    );
}

function useFirmenInfoFromContext(enabled: boolean): {
    tenant: FirmenInfoTenant | null;
    loading: boolean;
    missingTenant: boolean;
} {
    const currentTenant = useCurrentTenant();
    const tenantInfo = useTenantInfo();

    if (!enabled) {
        return { tenant: null, loading: false, missingTenant: false };
    }

    if (currentTenant.isTenantRecordLoading || tenantInfo.isLoading) {
        return { tenant: null, loading: true, missingTenant: false };
    }

    if (!currentTenant.tenantId || !currentTenant.isRealTenantSlug) {
        return { tenant: null, loading: false, missingTenant: true };
    }

    const slug = tenantInfo.tenantSlug ?? currentTenant.tenantSlug ?? '';
    if (!slug) {
        return { tenant: null, loading: false, missingTenant: true };
    }

    return {
        tenant: {
            id: currentTenant.tenantId,
            name:
                currentTenant.tenantName?.trim()
                || tenantInfo.tenantName?.trim()
                || formatTenantDisplay({
                    name: tenantInfo.tenantName ?? '',
                    slug,
                }).displayName,
            slug,
            createdAt: tenantInfo.registeredAt,
            licenseStatus: tenantInfo.licenseStatus,
        },
        loading: false,
        missingTenant: false,
    };
}

export function FirmenInfo({ tenant: tenantOverride, loading: loadingOverride }: FirmenInfoProps) {
    const { t, formatLocale } = useI18n();
    const fromContext = useFirmenInfoFromContext(tenantOverride === undefined);
    const loading = loadingOverride ?? (tenantOverride === undefined && fromContext.loading);
    const tenant = tenantOverride === undefined ? fromContext.tenant : tenantOverride;

    if (loading) {
        return (
            <Card title={t('common.tenant.companyInfo')}>
                <div style={{ display: 'flex', justifyContent: 'center', padding: 24 }}>
                    <Spin />
                </div>
            </Card>
        );
    }

    if (tenantOverride === undefined && fromContext.missingTenant) {
        return (
            <Alert
                type="warning"
                showIcon
                title={t('adminShell.tenant.selectTenantFirstTitle')}
                description={t('adminShell.tenant.selectTenantFirstBody')}
            />
        );
    }

    if (!tenant) {
        return null;
    }

    const mandantLabel = `${t('common.tenant.tenant')} (${t('common.tenant.tenantAlt')})`;
    const displayName = tenant.name?.trim() || tenant.slug;

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
                    <LicenseStatusTag licenseStatus={tenant.licenseStatus} />
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.registeredAt')}>
                    {tenant.createdAt
                        ? formatDate(tenant.createdAt, formatLocale, {
                              year: 'numeric',
                              month: '2-digit',
                              day: '2-digit',
                          })
                        : '—'}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}
