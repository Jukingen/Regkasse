'use client';

import { ClockCircleOutlined, LockOutlined } from '@ant-design/icons';
import { Button, Card, Flex, Progress, Typography } from 'antd';
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

/**
 * Dashboard widget for active mandant license grace — remaining days + lockdown timeline.
 */
export function GracePeriodWidget() {
  const { t } = useI18n();
  const router = useRouter();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });

  if (isLoading || !status) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;
  if (status.state !== 'Grace') return null;

  const daysLeft = status.graceDaysRemaining;
  const isUrgent = isGracePeriodBannerUrgent(status);
  const percent = getGracePeriodConsumedPercent(daysLeft, TENANT_GRACE_PERIOD_DAYS);
  const accent = isUrgent ? '#cf1322' : '#d48806';
  const borderColor = isUrgent ? '#ffa39e' : '#ffe58f';
  const background = isUrgent ? '#fff1f0' : '#fffbe6';

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
      style={{
        marginBottom: 16,
        borderColor,
        borderWidth: 2,
        background,
      }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Flex align="center" justify="space-between" gap={16} wrap="wrap">
        <div style={{ flex: 1, minWidth: 220 }}>
          <Typography.Title level={5} style={{ margin: 0, color: accent }}>
            <ClockCircleOutlined style={{ marginInlineEnd: 8 }} aria-hidden />
            {t('license.gracePeriodWidget.title')}
          </Typography.Title>
          <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0, color: accent }}>
            {t('license.gracePeriodWidget.description', { days: daysLeft })}
          </Typography.Paragraph>
        </div>
        <div style={{ textAlign: 'center', minWidth: 96 }}>
          <Typography.Title
            level={2}
            style={{ margin: 0, color: accent, fontVariantNumeric: 'tabular-nums' }}
          >
            {daysLeft}
          </Typography.Title>
          <Typography.Text style={{ fontSize: 12, color: accent }}>
            {t('license.gracePeriodWidget.daysRemaining')}
          </Typography.Text>
        </div>
      </Flex>

      <Progress
        percent={percent}
        strokeColor={accent}
        size="small"
        showInfo={false}
        style={{ marginTop: 12, marginBottom: 0 }}
        aria-label={t('license.gracePeriodWidget.progressAria', {
          days: daysLeft,
          total: TENANT_GRACE_PERIOD_DAYS,
        })}
      />

      <Flex justify="space-between" gap={8} wrap="wrap" style={{ marginTop: 8 }}>
        <Typography.Text style={{ fontSize: 12, color: accent }}>
          {t('license.gracePeriodWidget.labelExpired')}
        </Typography.Text>
        <Typography.Text style={{ fontSize: 12, color: accent }}>
          {t('license.gracePeriodWidget.labelGrace')}
        </Typography.Text>
        <Typography.Text style={{ fontSize: 12, color: accent }}>
          <LockOutlined style={{ marginInlineEnd: 4 }} aria-hidden />
          {t('license.gracePeriodWidget.labelLockdown')}
        </Typography.Text>
      </Flex>

      <div style={{ marginTop: 12 }}>
        <Button type="primary" danger={isUrgent} onClick={openRenewal}>
          {t('license.gracePeriodWidget.renew')}
        </Button>
      </div>
    </Card>
  );
}
