'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  KeyOutlined,
  LockOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, Flex, Progress, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';

import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { getGracePeriodConsumedPercent } from '@/features/license/utils/gracePeriodProgress';
import { isGracePeriodBannerUrgent } from '@/features/license/utils/gracePeriodUrgentWarning';
import {
  getLicenseCountdownAccentColor,
  getLicenseCountdownProgressPercent,
} from '@/features/license/utils/licenseCountdownWidget';
import { getLicenseExpiryImpactModel } from '@/features/license/utils/licenseExpiryImpact';
import {
  formatLicenseExpiryCountdown,
  getLicenseExpiryCountdownParts,
} from '@/features/license/utils/licenseExpiryCountdown';
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
import { useI18n } from '@/i18n';
import { formatGermanDate } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';

const COUNTDOWN_INTERVAL_MS = 60_000;

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
 * Combined mandant license status for the Manager dashboard:
 * countdown + health + compact impact + grace strip (when active).
 */
export function ManagerLicenseStatusCard() {
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

  const expiresAt = status?.expiredAt ?? null;
  const [timeLeft, setTimeLeft] = useState('');
  const [countdownDaysLeft, setCountdownDaysLeft] = useState(0);
  const [isExpired, setIsExpired] = useState(false);

  useEffect(() => {
    if (!expiresAt) return;

    const updateCountdown = () => {
      const parts = getLicenseExpiryCountdownParts(expiresAt);
      if (!parts || parts.totalMs <= 0) {
        setTimeLeft(t('dashboard.widgets.licenseStatus.expired'));
        setCountdownDaysLeft(0);
        setIsExpired(true);
        return;
      }

      setCountdownDaysLeft(parts.days);
      setTimeLeft(formatLicenseExpiryCountdown(expiresAt) ?? '');
      setIsExpired(false);
    };

    updateCountdown();
    const interval = window.setInterval(updateCountdown, COUNTDOWN_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [expiresAt, t]);

  if (!canView || isLoading || !status) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;

  const hasExpiry = Boolean(expiresAt?.trim());
  const daysLeft =
    status.state === 'Active'
      ? status.daysUntilExpiry
      : status.state === 'Grace'
        ? status.graceDaysRemaining
        : status.daysOverdue;

  const strokeColor = getLicenseHealthStrokeColor(status.state, daysLeft);
  const healthPercent = getLicenseHealthPercent({
    state: status.state,
    daysUntilExpiry: status.daysUntilExpiry,
    graceDaysRemaining: status.graceDaysRemaining,
    hasExpiry,
  });
  const countdownAccent = hasExpiry
    ? getLicenseCountdownAccentColor(isExpired, countdownDaysLeft)
    : strokeColor;
  const countdownProgress = hasExpiry
    ? getLicenseCountdownProgressPercent(isExpired, countdownDaysLeft)
    : healthPercent;

  const isHealthy = status.state === 'Active';
  const needsAction = !isHealthy;
  const borderColor = needsAction || isExpired ? strokeColor : countdownAccent;

  const impactModel = getLicenseExpiryImpactModel({
    state: status.state,
    daysUntilExpiry: status.daysUntilExpiry,
    graceDaysRemaining: status.graceDaysRemaining,
    daysOverdue: status.daysOverdue,
  });
  const impactAlertOk = impactModel.alertType === 'info';

  const currentDaysDescription =
    impactModel.currentDaysKind === 'untilExpiry'
      ? t('dashboard.widgets.licenseImpact.current.daysValid', {
          days: impactModel.currentDaysLabelValue,
        })
      : impactModel.currentDaysKind === 'graceRemaining'
        ? t('dashboard.widgets.licenseImpact.current.daysGrace', {
            days: impactModel.currentDaysLabelValue,
          })
        : t('dashboard.widgets.licenseImpact.current.daysOverdue', {
            days: impactModel.currentDaysLabelValue,
          });

  const inGrace = status.state === 'Grace';
  const graceDaysLeft = status.graceDaysRemaining;
  const graceUrgent = isGracePeriodBannerUrgent(status);
  const gracePercent = getGracePeriodConsumedPercent(graceDaysLeft, TENANT_GRACE_PERIOD_DAYS);
  const graceAccent = graceUrgent ? '#cf1322' : '#d48806';

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
      title={t('dashboard.widgets.licenseStatus.title')}
      style={{
        marginBottom: 16,
        borderColor,
        borderWidth: needsAction || isExpired ? 2 : 1,
      }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Flex align="center" justify="space-between" gap={16} wrap="wrap">
        <Flex align="center" gap={12} style={{ minWidth: 160 }}>
          {healthStatusIcon(status.state, strokeColor)}
          <div>
            <Typography.Text type="secondary">
              {t('dashboard.widgets.licenseStatus.status')}
            </Typography.Text>
            <Typography.Title level={5} style={{ margin: 0, color: strokeColor }}>
              {t(`dashboard.widgets.licenseHealth.states.${status.state}`)}
            </Typography.Title>
            <Typography.Text type={isHealthy ? 'success' : 'danger'}>
              {isHealthy
                ? t('dashboard.widgets.licenseStatus.healthy')
                : t('dashboard.widgets.licenseStatus.actionNeeded')}
            </Typography.Text>
          </div>
        </Flex>

        <div style={{ minWidth: 140 }}>
          <Typography.Text type="secondary">
            {t('dashboard.widgets.licenseStatus.validUntil')}
          </Typography.Text>
          <Typography.Title level={5} style={{ margin: 0 }}>
            {hasExpiry && expiresAt ? formatGermanDate(expiresAt) : '—'}
          </Typography.Title>
          {hasExpiry ? (
            <>
              <Typography.Title
                level={4}
                style={{ margin: '4px 0 0', color: countdownAccent, fontVariantNumeric: 'tabular-nums' }}
              >
                {timeLeft}
              </Typography.Title>
              <Typography.Text type="secondary">
                {isExpired ? (
                  <>
                    <WarningOutlined style={{ marginInlineEnd: 6 }} aria-hidden />
                    {t('dashboard.widgets.licenseStatus.expired')}
                  </>
                ) : (
                  t('dashboard.widgets.licenseStatus.remainingHint')
                )}
              </Typography.Text>
            </>
          ) : null}
        </div>

        <Button
          type={needsAction || isExpired ? 'primary' : 'default'}
          danger={status.state === 'Locked' || status.state === 'Archived' || isExpired}
          icon={<KeyOutlined />}
          onClick={openRenewal}
        >
          {needsAction || isExpired
            ? t('dashboard.widgets.licenseStatus.extendNow')
            : t('dashboard.widgets.licenseStatus.extend')}
        </Button>
      </Flex>

      <Progress
        percent={hasExpiry ? countdownProgress : healthPercent}
        strokeColor={hasExpiry ? countdownAccent : strokeColor}
        size="small"
        showInfo={hasExpiry || status.state === 'Grace'}
        format={() => t('dashboard.widgets.licenseStatus.daysLeft', { days: daysLeft })}
        style={{ marginTop: 12, marginBottom: 0 }}
        aria-label={t('dashboard.widgets.licenseStatus.progressAria', {
          days: daysLeft,
        })}
      />

      {inGrace ? (
        <div
          style={{
            marginTop: 16,
            padding: 12,
            borderRadius: 8,
            border: `1px solid ${graceUrgent ? '#ffa39e' : '#ffe58f'}`,
            background: graceUrgent ? '#fff1f0' : '#fffbe6',
          }}
        >
          <Flex align="center" justify="space-between" gap={12} wrap="wrap">
            <div style={{ flex: 1, minWidth: 200 }}>
              <Typography.Text strong style={{ color: graceAccent }}>
                <ClockCircleOutlined style={{ marginInlineEnd: 8 }} aria-hidden />
                {t('license.gracePeriodWidget.title')}
              </Typography.Text>
              <Typography.Paragraph style={{ margin: '4px 0 0', color: graceAccent }}>
                {t('license.gracePeriodWidget.description', { days: graceDaysLeft })}
              </Typography.Paragraph>
            </div>
            <div style={{ textAlign: 'center', minWidth: 72 }}>
              <Typography.Title
                level={3}
                style={{ margin: 0, color: graceAccent, fontVariantNumeric: 'tabular-nums' }}
              >
                {graceDaysLeft}
              </Typography.Title>
              <Typography.Text style={{ fontSize: 12, color: graceAccent }}>
                {t('license.gracePeriodWidget.daysRemaining')}
              </Typography.Text>
            </div>
          </Flex>
          <Progress
            percent={gracePercent}
            strokeColor={graceAccent}
            size="small"
            showInfo={false}
            style={{ marginTop: 8, marginBottom: 0 }}
            aria-label={t('license.gracePeriodWidget.progressAria', {
              days: graceDaysLeft,
              total: TENANT_GRACE_PERIOD_DAYS,
            })}
          />
          <Flex justify="space-between" gap={8} wrap="wrap" style={{ marginTop: 8 }}>
            <Typography.Text style={{ fontSize: 12, color: graceAccent }}>
              {t('license.gracePeriodWidget.labelExpired')}
            </Typography.Text>
            <Typography.Text style={{ fontSize: 12, color: graceAccent }}>
              {t('license.gracePeriodWidget.labelGrace')}
            </Typography.Text>
            <Typography.Text style={{ fontSize: 12, color: graceAccent }}>
              <LockOutlined style={{ marginInlineEnd: 4 }} aria-hidden />
              {t('license.gracePeriodWidget.labelLockdown')}
            </Typography.Text>
          </Flex>
        </div>
      ) : null}

      <Alert
        type={impactModel.alertType}
        showIcon
        style={{ marginTop: 16 }}
        title={
          impactAlertOk
            ? t('dashboard.widgets.licenseImpact.alert.okTitle')
            : t('dashboard.widgets.licenseImpact.alert.actionTitle')
        }
        description={`${currentDaysDescription} — ${
          impactAlertOk
            ? t('dashboard.widgets.licenseImpact.alert.okDescription')
            : t('dashboard.widgets.licenseImpact.alert.actionDescription')
        }`}
      />
    </Card>
  );
}
