'use client';

import { Card, Descriptions, Flex, Tag, Tooltip, Typography } from 'antd';
import type { CSSProperties } from 'react';

import { useTenantInfo } from '@/features/tenant/hooks/useTenantInfo';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
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

function TenantModeTags({
    isDevTenantOverride,
    isImpersonating,
    isPlatformAdminHost,
}: {
    isDevTenantOverride: boolean;
    isImpersonating: boolean;
    isPlatformAdminHost: boolean;
}) {
    const { t } = useI18n();

    if (!isDevTenantOverride && !isImpersonating && !isPlatformAdminHost) {
        return null;
    }

    return (
        <Flex gap={4} wrap="wrap">
            {isDevTenantOverride ? (
                <Tag color="gold">{t('adminShell.tenant.infoCardDevOverrideTag')}</Tag>
            ) : null}
            {isImpersonating ? (
                <Tag color="purple">{t('adminShell.tenant.infoCardImpersonationTag')}</Tag>
            ) : null}
            {isPlatformAdminHost ? (
                <Tag color="orange">{t('adminShell.tenant.infoCardPlatformAdminTag')}</Tag>
            ) : null}
        </Flex>
    );
}

export function TenantInfoCard({ className, style }: TenantInfoCardProps) {
    const { t, formatLocale } = useI18n();
    const { requiresTenantSelection } = useSuperAdminTenantMode();
    const { jwtTenantSlug, isDevTenantOverride, isImpersonating, isPlatformAdminHost } =
        useTenantContext();
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

    if (requiresTenantSelection) {
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

    const normalizedSlug = tenantSlug?.trim().toLowerCase() ?? '';
    const normalizedJwtSlug = jwtTenantSlug?.trim().toLowerCase() ?? '';
    const showJwtSlug =
        normalizedJwtSlug.length > 0 && normalizedJwtSlug !== normalizedSlug;

    return (
        <Card
            className={className}
            style={{ marginBottom: 12, ...style }}
            title={
                <Flex vertical gap={2}>
                    <Typography.Text type="secondary" style={{ fontSize: 12, fontWeight: 400 }}>
                        {t('adminShell.tenant.infoCardEyebrow')}
                    </Typography.Text>
                    <span>{t('adminShell.tenant.infoCardTitle')}</span>
                </Flex>
            }
            size="small"
            loading={isLoading}
        >
            <TenantModeTags
                isDevTenantOverride={isDevTenantOverride}
                isImpersonating={isImpersonating}
                isPlatformAdminHost={isPlatformAdminHost}
            />
            <Descriptions column={1} size="small" style={{ marginTop: 8 }}>
                <Descriptions.Item label={t('adminShell.tenant.infoCardName')}>
                    {tenantName || '—'}
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.infoCardSlug')}>
                    <Typography.Text code copyable>
                        {tenantSlug}
                    </Typography.Text>
                </Descriptions.Item>
                {showJwtSlug ? (
                    <Descriptions.Item label={t('adminShell.tenant.infoCardJwtSlug')}>
                        <Typography.Text code copyable>
                            {jwtTenantSlug}
                        </Typography.Text>
                    </Descriptions.Item>
                ) : null}
                <Descriptions.Item label={t('adminShell.tenant.infoCardId')}>
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
