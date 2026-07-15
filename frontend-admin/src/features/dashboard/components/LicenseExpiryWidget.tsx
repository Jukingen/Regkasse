'use client';

import { useState } from 'react';
import { Alert, Button, Space, Spin, Statistic, Tag, Typography } from 'antd';
import { SafetyCertificateOutlined } from '@ant-design/icons';

import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { useTenantLicenseDetail } from '@/features/license/hooks/useTenantLicenseDetail';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    getLicenseStatusDayText,
    getLicenseStatusLabel,
    getLicenseStatusTagColor,
    resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useTenantLicense, tenantLicenseUnifiedQueryKey } from '@/hooks/useTenantLicense';
import { formatDate, useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useQueryClient } from '@tanstack/react-query';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

function resolveDaysAccent(daysRemaining: number, kind: string): string {
    if (kind === 'lockdown' || kind === 'expired' || kind === 'no_license') {
        return '#cf1322';
    }
    if (kind === 'grace_write' || kind === 'grace_readonly') {
        return '#d48806';
    }
    if (daysRemaining <= 7) {
        return '#cf1322';
    }
    if (daysRemaining <= 30) {
        return '#d48806';
    }
    return '#3f8600';
}

/** Mandant license expiry widget — unified `GET /api/license/status` read model. */
export function LicenseExpiryWidget({ title, dragHandleProps }: Props) {
    const { t, formatLocale } = useI18n();
    const queryClient = useQueryClient();
    const currentTenant = useCurrentTenant();
    const { isAuthorized: canView } = useAuthorizationGate({
        requiredPermission: PERMISSIONS.LICENSE_VIEW,
    });
    const { isAuthorized: canExtend } = useAuthorizationGate({
        requiredPermission: PERMISSIONS.LICENSE_MANAGE,
    });
    const [extendOpen, setExtendOpen] = useState(false);

    const tenantId = currentTenant.tenantId ?? '';

    const licenseQuery = useTenantLicense();
    const statusQuery = useTenantLicenseStatus();
    const detailQuery = useTenantLicenseDetail(canExtend ? tenantId : undefined);

    const resolvedStatus = statusQuery.data;
    const kind = resolvedStatus?.kind ?? 'no_license';
    const daysRemaining = resolvedStatus?.daysRemaining ?? 0;
    const detailStatus = detailQuery.data?.status;
    const validUntil =
        licenseQuery.data?.validUntil ?? detailStatus?.validUntilUtc ?? null;
    const extendResolvedStatus =
        detailStatus != null
            ? resolveTenantLicenseStatus(detailStatus)
            : resolvedStatus != null
              ? {
                    kind: resolvedStatus.kind,
                    daysRemaining: resolvedStatus.daysRemaining,
                    daysExpired: resolvedStatus.daysExpired,
                    canWrite: resolvedStatus.canWrite,
                    canManageUsers: resolvedStatus.canManageUsers,
                    canAccess: resolvedStatus.canAccess,
                }
              : null;

    const handleRefresh = () => {
        void licenseQuery.refetch();
        void statusQuery.refetch();
        if (canExtend) {
            void detailQuery.refetch();
        }
    };

    if (!canView || !currentTenant.isRealTenantSlug || currentTenant.isSuperAdminPlatformMode) {
        return null;
    }

    if ((licenseQuery.isLoading || statusQuery.isLoading) && !resolvedStatus) {
        return (
            <WidgetShell title={title} dragHandleProps={dragHandleProps}>
                <Spin />
            </WidgetShell>
        );
    }

    const statusLabel = getLicenseStatusLabel(kind, t);
    const remainingLabel =
        resolvedStatus != null ? getLicenseStatusDayText(resolvedStatus, t) : null;
    const showExpiryWarning =
        kind === 'active' && daysRemaining > 0 && daysRemaining <= 30;

    return (
        <>
            <WidgetShell
                title={title}
                dragHandleProps={dragHandleProps}
                onRefresh={handleRefresh}
                refreshing={licenseQuery.isFetching || statusQuery.isFetching}
                extra={<Tag color={getLicenseStatusTagColor(kind)}>{statusLabel}</Tag>}
            >
                <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
                    <Statistic
                        title={t('dashboard.widgets.licenseExpiry.daysRemaining')}
                        value={Math.max(0, daysRemaining)}
                        styles={{
                            content: {
                                color: resolveDaysAccent(daysRemaining, kind),
                            },
                        }}
                    />
                    {remainingLabel ? (
                        <Typography.Text type="secondary">{remainingLabel}</Typography.Text>
                    ) : null}
                    {validUntil ? (
                        <Typography.Text type="secondary">
                            {t('dashboard.widgets.licenseExpiry.validUntil', {
                                date: formatDate(validUntil, formatLocale),
                            })}
                        </Typography.Text>
                    ) : null}
                    {showExpiryWarning ? (
                        <Alert
                            type={daysRemaining <= 7 ? 'error' : 'warning'}
                            showIcon
                            title={
                                daysRemaining <= 7
                                    ? t('dashboard.widgets.licenseExpiry.expiresSoon7')
                                    : t('dashboard.widgets.licenseExpiry.expiresSoon30')
                            }
                        />
                    ) : null}
                    {kind !== 'active' && kind !== 'grace_write' ? (
                        <Alert
                            type="error"
                            showIcon
                            title={t('dashboard.widgets.licenseExpiry.actionRequired')}
                        />
                    ) : null}
                    {canExtend ? (
                        <Button
                            type="primary"
                            block
                            icon={<SafetyCertificateOutlined />}
                            onClick={() => setExtendOpen(true)}
                        >
                            {t('license.mandant.extendButton')}
                        </Button>
                    ) : null}
                </Space>
            </WidgetShell>

            {canExtend && tenantId ? (
                <LicenseExtendModal
                    open={extendOpen}
                    tenantId={tenantId}
                    status={detailStatus ?? null}
                    resolvedStatus={extendResolvedStatus}
                    onClose={() => setExtendOpen(false)}
                    onSuccess={() => {
                        void queryClient.invalidateQueries({ queryKey: tenantLicenseUnifiedQueryKey });
                        setExtendOpen(false);
                    }}
                />
            ) : null}
        </>
    );
}
