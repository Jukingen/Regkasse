'use client';

import { Card, Descriptions, Flex, Tag, Tooltip, Typography } from 'antd';
import type { CSSProperties } from 'react';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusMessage,
    getLicenseStatusTagColor,
} from '@/features/license/utils/licenseStatus';
import { useTenantInfo } from '@/features/tenant/hooks/useTenantInfo';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { formatDate, useI18n } from '@/i18n';

export type TenantInfoCardProps = {
    className?: string;
    style?: CSSProperties;
};

function LicenseStatusTag({
    licenseStatus,
}: {
    licenseStatus: ResolvedLicenseStatus;
}) {
    const { t } = useI18n();
    const tooltip = getLicenseStatusDayText(licenseStatus, t);
    const message = getLicenseStatusMessage(licenseStatus, 'tenant', t);
    const content = (
        <Tag color={getLicenseStatusTagColor(licenseStatus.kind)}>
            {getLicenseStatusLabel(licenseStatus.kind, t)}
        </Tag>
    );

    return (
        <Tooltip title={tooltip ? `${message} ${tooltip}` : message}>
            {content}
        </Tooltip>
    );
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
        licenseStatus,
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
                <Tooltip title={t('adminShell.tenant.infoCardEyebrowHint')}>
                    <Flex vertical gap={2}>
                        <Typography.Text type="secondary" style={{ fontSize: 12, fontWeight: 400 }}>
                            {t('adminShell.tenant.infoCardEyebrow')}
                        </Typography.Text>
                        <span>
                            {tenantName?.trim()
                                ? t('common.tenant.badgeDualLabel', { name: tenantName.trim() })
                                : t('adminShell.tenant.infoCardTitle')}
                        </span>
                    </Flex>
                </Tooltip>
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
                <Descriptions.Item
                    label={
                        <Tooltip title={t('common.tenant.tenantDescription')}>
                            {t('adminShell.tenant.infoCardId')}
                        </Tooltip>
                    }
                >
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
                    <LicenseStatusTag licenseStatus={licenseStatus} />
                </Descriptions.Item>
                <Descriptions.Item label={t('adminShell.tenant.info.registeredAt')}>
                    {registeredLabel}
                </Descriptions.Item>
            </Descriptions>
        </Card>
    );
}
