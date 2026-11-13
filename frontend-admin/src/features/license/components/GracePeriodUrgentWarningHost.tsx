'use client';

import { KeyOutlined, WarningOutlined } from '@ant-design/icons';
import { Button, Flex, Modal, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';

import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { formatGraceLockCountdown } from '@/features/license/utils/graceLockCountdown';
import {
  graceUrgentDismissStorageKey,
  isGraceUrgentDismissed,
  setGraceUrgentDismissed,
  shouldShowGraceUrgentWarning,
} from '@/features/license/utils/gracePeriodUrgentWarning';
import { redirectToLicensePayment } from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const TICK_MS = 1000;

/**
 * Blocking FA modal for the last ≤24h of mandant grace (before POS lockdown).
 * Dismiss is session-scoped per tenant + lock deadline.
 */
export function GracePeriodUrgentWarningHost() {
  const { t } = useI18n();
  const router = useRouter();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });

  const eligible = useMemo(() => {
    if (isLoading || !status) return false;
    if (tenant.suppressLicenseWarnings || !tenant.isRealTenantSlug) return false;
    if (tenant.isSuperAdminPlatformMode || !tenant.tenantId) return false;
    return shouldShowGraceUrgentWarning(status);
  }, [isLoading, status, tenant]);

  const storageKey = useMemo(() => {
    if (!tenant.tenantId) return null;
    return graceUrgentDismissStorageKey(tenant.tenantId, status?.graceEndedAt);
  }, [tenant.tenantId, status?.graceEndedAt]);

  const [dismissed, setDismissed] = useState(false);
  const [countdown, setCountdown] = useState('');

  useEffect(() => {
    if (!storageKey) {
      setDismissed(false);
      return;
    }
    setDismissed(isGraceUrgentDismissed(storageKey));
  }, [storageKey]);

  useEffect(() => {
    if (!eligible || dismissed) return;
    const lockAt = status?.graceEndedAt ?? null;

    const tick = () => {
      setCountdown(formatGraceLockCountdown(lockAt) ?? t('license.graceUrgentWarning.countdownUnknown'));
    };
    tick();
    const id = window.setInterval(tick, TICK_MS);
    return () => window.clearInterval(id);
  }, [eligible, dismissed, status?.graceEndedAt, t]);

  const open = eligible && !dismissed;

  const dismiss = () => {
    if (storageKey) setGraceUrgentDismissed(storageKey);
    setDismissed(true);
  };

  const openRenewal = () => {
    if (canExtend && tenant.tenantId) {
      openLicenseRenewalModal();
      dismiss();
      return;
    }
    redirectToLicensePayment({
      isSuperAdmin: tenant.isSuperAdminUser,
      pushInternal: (href) => router.push(href),
    });
    dismiss();
  };

  return (
    <Modal
      title={t('license.graceUrgentWarning.title')}
      open={open}
      closable={false}
      mask={{ closable: false }}
      keyboard={false}
      footer={null}
      destroyOnHidden
      centered
      width={480}
    >
      <Flex vertical align="center" gap={16} style={{ textAlign: 'center', paddingBlock: 8 }}>
        <WarningOutlined style={{ fontSize: 56, color: '#cf1322' }} aria-hidden />
        <Typography.Title level={4} style={{ margin: 0, color: '#cf1322' }}>
          {t('license.graceUrgentWarning.heading')}
        </Typography.Title>
        <Typography.Paragraph style={{ margin: 0 }}>
          {t('license.graceUrgentWarning.description')}
        </Typography.Paragraph>

        <div
          style={{
            width: '100%',
            padding: 16,
            borderRadius: 8,
            background: '#fff1f0',
            border: '1px solid #ffa39e',
          }}
        >
          <Flex justify="space-between" align="center" gap={12} wrap="wrap">
            <Typography.Text>{t('license.graceUrgentWarning.remainingLabel')}</Typography.Text>
            <Typography.Text strong style={{ color: '#cf1322', fontVariantNumeric: 'tabular-nums' }}>
              {countdown}
            </Typography.Text>
          </Flex>
        </div>

        <Button type="primary" danger size="large" icon={<KeyOutlined />} block onClick={openRenewal}>
          {t('license.graceUrgentWarning.renewNow')}
        </Button>
        <Button block onClick={dismiss}>
          {t('license.graceUrgentWarning.acknowledge')}
        </Button>
      </Flex>
    </Modal>
  );
}
