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

import { useI18n, formatDate } from '@/i18n';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenantLicense } from '@/features/license/hooks/useTenantLicense';
import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusTagColor,
    resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';

const EXPIRING_SOON_THRESHOLD_DAYS = 7;

export function TenantLicenseSection() {
    const { t, formatLocale } = useI18n();
    const currentTenant = useCurrentTenant();
    const [extendOpen, setExtendOpen] = useState(false);

    const tenantId = currentTenant.tenantId ?? '';
    const licenseQuery = useTenantLicense(tenantId);

    const status = licenseQuery.data?.status;
    const resolvedStatus = status ? resolveTenantLicenseStatus(status) : null;

    const expiryBanner = useMemo(() => {
        if (!resolvedStatus) return null;
        const days = resolvedStatus.daysRemaining;
        if (days > 0 && days <= EXPIRING_SOON_THRESHOLD_DAYS) {
            return (
                <Alert
                    type="warning"
                    showIcon
                    title={t('license.tenant.expiresSoon')}
                    description={getLicenseStatusDayText(resolvedStatus, t) ?? undefined}
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
                    title={t('license.tenant.expired')}
                    description={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
                />
            );
        }
        return null;
    }, [resolvedStatus, t]);

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
            <Typography.Title level={4} style={{ margin: 0 }}>
                {t('license.page.tenantLicense')}
            </Typography.Title>

            {expiryBanner}

            <Card loading={licenseQuery.isLoading}>
                {!licenseQuery.isLoading && !status ? (
                    <Empty description={t('license.tenant.noLicense')} />
                ) : null}
                {status ? (
                    <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
                        <Descriptions.Item label={t('license.tenant.status')}>
                            <Tag color={getLicenseStatusTagColor(resolvedStatus?.kind ?? 'no_license')}>
                                {getLicenseStatusLabel(resolvedStatus?.kind ?? 'no_license', t)}
                            </Tag>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.tenant.licenseKey')}>
                            <Typography.Text
                                code
                                copyable={status.licenseKey ? { text: status.licenseKey } : undefined}
                            >
                                {maskTenantLicenseKey(status.licenseKey)}
                            </Typography.Text>
                        </Descriptions.Item>
                        <Descriptions.Item label={t('license.tenant.validUntil')}>
                            {status.validUntilUtc ? formatDate(status.validUntilUtc, formatLocale) : '—'}
                        </Descriptions.Item>
                        {resolvedStatus ? (
                            <Descriptions.Item label={t('tenants.detail.license.remaining')}>
                                {getLicenseStatusDayText(resolvedStatus, t) ?? '—'}
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
                        {t('license.tenant.extendButton')}
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
        </Space>
    );
}
