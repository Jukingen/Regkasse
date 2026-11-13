'use client';

import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  KeyOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Button, Card, Flex, Progress, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import type { ReactNode } from 'react';

import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import {
  getLicenseHealthPercent,
  getLicenseHealthStrokeColor,
} from '@/features/license/utils/licenseHealthWidget';
import { redirectToLicensePayment } from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import {
  type LicenseLifecycleUiState,
  useLicenseStatus,
} from '@/hooks/useLicenseStatus';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

function healthStatusIcon(state: LicenseLifecycleUiState, color: string): ReactNode {
  switch (state) {
    case 'Active':
      return <CheckCircleOutlined style={{ fontSize: 28, color }} aria-hidden />;
    case 'Grace':
      return <WarningOutlined style={{ fontSize: 28, color }} aria-hidden />;
    case 'Locked':
    case 'Archived':
    default:
      return <CloseCircleOutlined style={{ fontSize: 28, color }} aria-hidden />;
  }
}

/**
 * Compact mandant license health summary for the Manager dashboard.
 * Complements the countdown (timer) and grace detail cards.
 */
export function LicenseHealthWidget() {
  const { t } = useI18n();
  const router = useRouter();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });
  const { isAuthorized: canView } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_VIEW,
  });

  if (!canView || isLoading || !status) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;

  const hasExpiry = Boolean(status.expiredAt?.trim());
  const daysLeft =
    status.state === 'Active'
      ? status.daysUntilExpiry
      : status.state === 'Grace'
        ? status.graceDaysRemaining
        : status.daysOverdue;

  const strokeColor = getLicenseHealthStrokeColor(status.state, daysLeft);
  const percent = getLicenseHealthPercent({
    state: status.state,
    daysUntilExpiry: status.daysUntilExpiry,
    graceDaysRemaining: status.graceDaysRemaining,
    hasExpiry,
  });

  const isHealthy = status.state === 'Active';
  const needsAction = !isHealthy;

  const openRenewal = () => {
    if (canExtend && tenant.tenantId) {
      openLicenseRenewalModal();
      return;
    }
    redirectToLicensePayment({
      isSuperAdmin: tenant.isSuperAdminUser,
      pushInternal: (href) => router.push(href),
    });
  };

  return (
    <Card
      size="small"
      title={t('dashboard.widgets.licenseHealth.title')}
      style={{ marginBottom: 16, borderColor: strokeColor, borderWidth: needsAction ? 2 : 1 }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Flex align="center" justify="space-between" gap={16} wrap="wrap">
        <Flex align="center" gap={12} style={{ minWidth: 160 }}>
          {healthStatusIcon(status.state, strokeColor)}
          <div>
            <Typography.Text type="secondary">
              {t('dashboard.widgets.licenseHealth.statusLabel')}
            </Typography.Text>
            <Typography.Title level={5} style={{ margin: 0, color: strokeColor }}>
              {t(`dashboard.widgets.licenseHealth.states.${status.state}`)}
            </Typography.Title>
          </div>
        </Flex>

        <div style={{ flex: 1, minWidth: 200 }}>
          <Typography.Text type="secondary">
            {t('dashboard.widgets.licenseHealth.validUntil')}
          </Typography.Text>
          <Typography.Title level={5} style={{ margin: '0 0 8px' }}>
            {hasExpiry ? formatLicenseValidUntil(status.expiredAt) : '—'}
          </Typography.Title>
          <Progress
            percent={hasExpiry ? percent : 0}
            strokeColor={strokeColor}
            size="small"
            showInfo={hasExpiry}
            format={() =>
              t('dashboard.widgets.licenseHealth.daysLeft', { days: daysLeft })
            }
            aria-label={t('dashboard.widgets.licenseHealth.progressAria', {
              days: daysLeft,
            })}
          />
          <Typography.Text type={isHealthy ? 'success' : 'danger'}>
            {isHealthy
              ? t('dashboard.widgets.licenseHealth.healthy')
              : t('dashboard.widgets.licenseHealth.actionNeeded')}
          </Typography.Text>
        </div>

        <Button
          type={needsAction ? 'primary' : 'default'}
          danger={status.state === 'Locked' || status.state === 'Archived'}
          icon={<KeyOutlined />}
          onClick={openRenewal}
        >
          {needsAction
            ? t('dashboard.widgets.licenseHealth.extendNow')
            : t('dashboard.widgets.licenseHealth.extend')}
        </Button>
      </Flex>
    </Card>
  );
}
