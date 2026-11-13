'use client';

import { ClockCircleOutlined, KeyOutlined, WarningOutlined } from '@ant-design/icons';
import { Button, Card, Flex, Progress, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import {
  getLicenseCountdownAccentColor,
  getLicenseCountdownProgressPercent,
} from '@/features/license/utils/licenseCountdownWidget';
import {
  formatLicenseExpiryCountdown,
  getLicenseExpiryCountdownParts,
} from '@/features/license/utils/licenseExpiryCountdown';
import { redirectToLicensePayment } from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const COUNTDOWN_INTERVAL_MS = 60_000;

/**
 * Compact mandant license expiry countdown for the FA dashboard header.
 * Opens the shared renewal modal when the license has expired.
 */
export function LicenseCountdownWidget() {
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
  const [daysLeft, setDaysLeft] = useState(0);
  const [isExpired, setIsExpired] = useState(false);

  useEffect(() => {
    if (!expiresAt) return;

    const updateCountdown = () => {
      const parts = getLicenseExpiryCountdownParts(expiresAt);
      if (!parts || parts.totalMs <= 0) {
        setTimeLeft(t('dashboard.widgets.licenseCountdown.expired'));
        setDaysLeft(0);
        setIsExpired(true);
        return;
      }

      setDaysLeft(parts.days);
      setTimeLeft(formatLicenseExpiryCountdown(expiresAt) ?? '');
      setIsExpired(false);
    };

    updateCountdown();
    const interval = window.setInterval(updateCountdown, COUNTDOWN_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [expiresAt, t]);

  if (!canView || isLoading || !status || !expiresAt) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;

  const accent = getLicenseCountdownAccentColor(isExpired, daysLeft);
  const progressPercent = getLicenseCountdownProgressPercent(isExpired, daysLeft);

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
      style={{ borderColor: accent, borderWidth: 2, marginBottom: 16 }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Flex align="center" justify="space-between" gap={16} wrap="wrap">
        <Flex align="center" gap={16}>
          <ClockCircleOutlined style={{ fontSize: 32, color: accent }} aria-hidden />
          <div>
            <Typography.Text type="secondary">
              {t('dashboard.widgets.licenseCountdown.validUntilLabel')}
            </Typography.Text>
            <Typography.Title level={4} style={{ margin: 0 }}>
              {formatLicenseValidUntil(expiresAt)}
            </Typography.Title>
          </div>
        </Flex>

        <div style={{ textAlign: 'center', minWidth: 140 }}>
          <Typography.Title level={2} style={{ margin: 0, color: accent }}>
            {timeLeft}
          </Typography.Title>
          <Typography.Text type="secondary">
            {isExpired ? (
              <>
                <WarningOutlined style={{ marginInlineEnd: 6 }} aria-hidden />
                {t('dashboard.widgets.licenseCountdown.expiredHint')}
              </>
            ) : (
              t('dashboard.widgets.licenseCountdown.remainingHint')
            )}
          </Typography.Text>
        </div>

        {isExpired ? (
          <Button type="primary" danger icon={<KeyOutlined />} onClick={openRenewal}>
            {t('dashboard.widgets.licenseCountdown.renewNow')}
          </Button>
        ) : null}
      </Flex>

      <Progress
        percent={progressPercent}
        strokeColor={accent}
        size="small"
        showInfo={false}
        style={{ marginTop: 12, marginBottom: 0 }}
        aria-label={t('dashboard.widgets.licenseCountdown.progressAria', {
          days: daysLeft,
        })}
      />
    </Card>
  );
}
