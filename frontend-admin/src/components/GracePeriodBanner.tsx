'use client';

import { ClockCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Flex, Progress, Typography } from 'antd';
import { useRouter } from 'next/navigation';

import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { getGracePeriodConsumedPercent } from '@/features/license/utils/gracePeriodProgress';
import { isGracePeriodBannerUrgent } from '@/features/license/utils/gracePeriodUrgentWarning';
import { redirectToLicensePayment } from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Text } = Typography;

/**
 * Global FA banner while mandant license is in the grace window.
 * Escalates to error styling in the last {@link GRACE_BANNER_URGENT_DAYS} days.
 */
export function GracePeriodBanner() {
  const { t } = useI18n();
  const router = useRouter();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });

  if (isLoading || !status) return null;
  if (tenant.suppressLicenseWarnings || !tenant.isRealTenantSlug) return null;
  if (status.state !== 'Grace') return null;

  const daysLeft = status.graceDaysRemaining;
  const isUrgent = isGracePeriodBannerUrgent(status);
  const percent = getGracePeriodConsumedPercent(daysLeft, TENANT_GRACE_PERIOD_DAYS);
  const strokeColor = isUrgent ? '#cf1322' : '#faad14';

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
    <Alert
      type={isUrgent ? 'error' : 'warning'}
      banner
      showIcon
      icon={isUrgent ? <WarningOutlined /> : <ClockCircleOutlined />}
      style={{ marginBottom: 12 }}
      title={
        <Flex align="center" justify="space-between" gap={12} wrap="wrap" style={{ width: '100%' }}>
          <Text strong>
            {isUrgent
              ? t('license.gracePeriodBanner.titleUrgent', { days: daysLeft })
              : t('license.gracePeriodBanner.title', { days: daysLeft })}
          </Text>
          <Button
            type={isUrgent ? 'primary' : 'default'}
            danger={isUrgent}
            size="small"
            onClick={openRenewal}
          >
            {isUrgent
              ? t('license.gracePeriodBanner.renewUrgent')
              : t('license.gracePeriodBanner.renew')}
          </Button>
        </Flex>
      }
      description={
        <Flex vertical gap={8} style={{ marginTop: 4 }}>
          <Flex align="center" gap={16} wrap="wrap">
            <Text type="secondary">
              {t('license.gracePeriodBanner.remainingLabel', {
                days: daysLeft,
                total: TENANT_GRACE_PERIOD_DAYS,
              })}
            </Text>
            <Progress
              percent={percent}
              size="small"
              strokeColor={strokeColor}
              showInfo={false}
              style={{ flex: 1, minWidth: 120, margin: 0 }}
              aria-label={t('license.gracePeriodBanner.progressAria', {
                days: daysLeft,
                total: TENANT_GRACE_PERIOD_DAYS,
              })}
            />
          </Flex>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('license.gracePeriodBanner.lockdownHint')}
          </Text>
        </Flex>
      }
    />
  );
}
