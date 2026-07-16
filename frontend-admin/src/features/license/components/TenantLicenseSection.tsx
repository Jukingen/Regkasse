'use client';

import { useMemo, useState } from 'react';
import {
    Alert,
    Button,
    Card,
    Descriptions,
    Empty,
    Space,
    Tag,
    Typography,
} from 'antd';

import { useI18n, formatGermanDateTime } from '@/i18n';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { useTenantLicense } from '@/hooks/useTenantLicense';
import { useTenantLicenseDetail } from '@/features/license/hooks/useTenantLicenseDetail';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { LicenseHistory } from '@/features/license/components/LicenseHistory';
import { FirmenInfo } from '@/features/tenants/components/FirmenInfo';
import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import {
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusRemainingText,
    getLicenseStatusTagColor,
    mapPublicStatusToTenantLicenseStatus,
    resolveTenantLicenseFromPublicStatus,
    resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';

const EXPIRING_SOON_THRESHOLD_DAYS = 7;

export function TenantLicenseSection() {
    const { t } = useI18n();
    const currentTenant = useCurrentTenant();
    const { tenant, isLoading: tenantLoading, error: tenantError } = useTenant();
    const { hasPermission } = usePermissions();
    const [extendOpen, setExtendOpen] = useState(false);

    const tenantId = currentTenant.tenantId ?? '';
    const isSuperAdmin = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

    const publicLicenseQuery = useTenantLicense(tenantId, {
        enabled: !isSuperAdmin && Boolean(tenantId),
    });
    const adminLicenseQuery = useTenantLicenseDetail(tenantId, {
        enabled: isSuperAdmin && Boolean(tenantId),
    });

    const licenseQuery = isSuperAdmin ? adminLicenseQuery : publicLicenseQuery;

    const status = useMemo(() => {
        if (isSuperAdmin) {
            return adminLicenseQuery.data?.status ?? null;
        }
        if (!publicLicenseQuery.data) {
            return null;
        }
        return mapPublicStatusToTenantLicenseStatus(publicLicenseQuery.data);
    }, [isSuperAdmin, adminLicenseQuery.data, publicLicenseQuery.data]);

    const resolvedStatus = useMemo(() => {
        if (isSuperAdmin) {
            return status ? resolveTenantLicenseStatus(status) : null;
        }
        return publicLicenseQuery.data
            ? resolveTenantLicenseFromPublicStatus(publicLicenseQuery.data)
            : null;
    }, [isSuperAdmin, status, publicLicenseQuery.data]);

    const remainingText = useMemo(() => {
        if (!resolvedStatus) return null;
        return getLicenseStatusRemainingText(resolvedStatus, t, status?.validUntilUtc);
    }, [resolvedStatus, status?.validUntilUtc, t]);

    const expiryBanner = useMemo(() => {
        if (!resolvedStatus) return null;
        const days = resolvedStatus.daysRemaining;
        if (days > 0 && days <= EXPIRING_SOON_THRESHOLD_DAYS) {
            return (
                <Alert
                    type="warning"
                    showIcon
                    title={t('license.mandant.expiresSoon')}
                    description={remainingText ?? undefined}
                />
            );
        }
        if (
            resolvedStatus.kind === 'grace_write' ||
            resolvedStatus.kind === 'grace_readonly' ||
            resolvedStatus.kind === 'lockdown' ||
            resolvedStatus.kind === 'no_license' ||
            days <= 0
        ) {
            return (
                <Alert
                    type="error"
                    showIcon
                    title={t('license.mandant.expired')}
                    description={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
                />
            );
        }
        return null;
    }, [resolvedStatus, remainingText, t]);

    const firmenInfo = (
        <FirmenInfo
            tenant={tenant}
            loading={tenantLoading || (currentTenant.isTenantRecordLoading && !tenantId)}
            error={tenantError}
            licenseValidUntilUtc={status?.validUntilUtc}
        />
    );

    if ((currentTenant.isTenantRecordLoading && !tenantId) || !tenantId) {
        return (
            <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                {firmenInfo}
            </Space>
        );
    }

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            {firmenInfo}

            <Typography.Title level={4} style={{ margin: 0 }}>
                {t('license.page.tenantLicense')}
            </Typography.Title>

            {expiryBanner}

            <Card loading={licenseQuery.isLoading}>
                {!licenseQuery.isLoading && !status ? (
                    <Empty description={t('license.mandant.noLicense')} />
                ) : null}
                {status ? (
                    <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
                        <Descriptions.Item label={t('license.mandant.status')}>
                            <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                                {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                            </Tag>
                        </Descriptions.Item>
                        {isSuperAdmin ? (
                            <Descriptions.Item label={t('license.mandant.licenseKey')}>
                                <Typography.Text
                                    code
                                    copyable={status.licenseKey ? { text: status.licenseKey } : undefined}
                                >
                                    {maskTenantLicenseKey(status.licenseKey)}
                                </Typography.Text>
                            </Descriptions.Item>
                        ) : null}
                        <Descriptions.Item label={t('license.mandant.validUntil')}>
                            {formatGermanDateTime(status.validUntilUtc)}
                        </Descriptions.Item>
                        {resolvedStatus ? (
                            <Descriptions.Item label={t('tenants.detail.license.remaining')}>
                                {remainingText ?? '—'}
                            </Descriptions.Item>
                        ) : null}
                    </Descriptions>
                ) : null}
                {resolvedStatus?.kind === 'active' && !expiryBanner ? (
                    <Alert
                        style={{ marginTop: 16 }}
                        type="success"
                        showIcon
                        title={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
                    />
                ) : null}
                <div style={{ marginTop: 16 }}>
                    <Button type="primary" onClick={() => setExtendOpen(true)}>
                        {t('license.mandant.extendButton')}
                    </Button>
                </div>
            </Card>

            <LicenseExtendModal
                open={extendOpen}
                tenantId={tenantId}
                status={status}
                resolvedStatus={resolvedStatus}
                onClose={() => setExtendOpen(false)}
            />

            <LicenseHistory tenantId={tenantId} />
        </Space>
    );
}
