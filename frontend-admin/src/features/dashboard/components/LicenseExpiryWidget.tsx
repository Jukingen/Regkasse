'use client';

import { SafetyCertificateOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Descriptions, Skeleton, Space, Tag } from 'antd';
import { useState } from 'react';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { LicenseExpiryCountdownText } from '@/features/license/components/LicenseExpiryCountdownText';
import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { useTenantLicenseDetail } from '@/features/license/hooks/useTenantLicenseDetail';
import {
  getLicenseStatusLabel,
  getLicenseStatusTagColor,
  resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
  getHeaderLicenseTooltipStatusLabel,
  getLicenseHoursRemaining,
} from '@/features/tenant/utils/headerLicenseStatus';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { tenantLicenseUnifiedQueryKey, useTenantLicense } from '@/hooks/useTenantLicense';
import { useI18n } from '@/i18n';
import { formatGermanDateTime } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

function isLicenseValidForWidget(kind: string, daysRemaining: number): boolean {
  return kind === 'active' && daysRemaining > 0;
}

/** Mandant license expiry widget — unified `GET /api/license/status` read model. */
export function LicenseExpiryWidget({ title, dragHandleProps }: Props) {
  const { t } = useI18n();
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
  const validUntil = licenseQuery.data?.validUntil ?? detailStatus?.validUntilUtc ?? null;
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
        <Skeleton active paragraph={{ rows: 3 }} />
      </WidgetShell>
    );
  }

  const statusLabel = getLicenseStatusLabel(kind, t);
  const showExpiryWarning = kind === 'active' && daysRemaining > 0 && daysRemaining <= 30;
  // Grace: show remaining grace days (≤7), never ValidUntil horizon.
  const displayDaysRemaining =
    kind === 'grace_write' || kind === 'grace_readonly'
      ? (resolvedStatus?.daysRemainingInGrace ?? 0)
      : Math.max(0, daysRemaining);
  const hoursRemaining =
    kind === 'grace_write' || kind === 'grace_readonly'
      ? 0
      : (getLicenseHoursRemaining(validUntil) ?? 0);
  const isValid = resolvedStatus != null && isLicenseValidForWidget(kind, daysRemaining);
  const detailStatusLabel =
    resolvedStatus != null ? getHeaderLicenseTooltipStatusLabel(resolvedStatus, t) : statusLabel;
  const validUntilDisplay =
    licenseQuery.data?.validUntilFormatted ?? (validUntil ? formatGermanDateTime(validUntil) : '—');

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
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label={t('dashboard.widgets.licenseExpiry.validUntilLabel')}>
              {validUntilDisplay}
            </Descriptions.Item>
            <Descriptions.Item label={t('dashboard.widgets.licenseExpiry.remainingLabel')}>
              {t('dashboard.widgets.licenseExpiry.remainingValue', {
                days: displayDaysRemaining,
                hours: hoursRemaining,
              })}
            </Descriptions.Item>
            <Descriptions.Item label={t('dashboard.widgets.licenseExpiry.statusLabel')}>
              <Tag color={isValid ? 'green' : 'red'}>{detailStatusLabel}</Tag>
            </Descriptions.Item>
          </Descriptions>
          <LicenseExpiryCountdownText
            expiresAt={validUntil}
            labelKey="dashboard.widgets.licenseExpiry.countdown"
            t={t}
          />
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
