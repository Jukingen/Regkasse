'use client';

import { KeyOutlined } from '@ant-design/icons';
import { Alert, Button, Flex, Space } from 'antd';
import { useEffect, useState } from 'react';

import {
  openLicenseRenewalModal,
  useLicenseRenewalModalStore,
} from '@/features/license/stores/licenseRenewalModalStore';
import {
  clearLicenseRenewalPending,
  isLicenseRenewalPending,
} from '@/features/license/utils/licenseRenewalRecoveryStorage';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Banner when a mandant renewal was started but not finished (localStorage, 1h TTL).
 * Hidden while the license is still Active (proactive renewals are not "interrupted").
 */
export function LicenseRenewalRecoveryBanner() {
  const { t } = useI18n();
  const tenant = useCurrentTenant();
  const { status } = useLicenseStatus();
  const modalOpen = useLicenseRenewalModalStore((s) => s.open);
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });
  const [showRecoveryBanner, setShowRecoveryBanner] = useState(false);

  useEffect(() => {
    if (!canExtend || !tenant.tenantId || !tenant.isRealTenantSlug) {
      setShowRecoveryBanner(false);
      return;
    }
    if (tenant.isSuperAdminPlatformMode || tenant.suppressLicenseWarnings) {
      setShowRecoveryBanner(false);
      return;
    }
    if (modalOpen) {
      setShowRecoveryBanner(false);
      return;
    }
    // Stale pending from proactive renewal while still Active — clear and hide.
    if (status?.state === 'Active') {
      if (isLicenseRenewalPending(tenant.tenantId)) {
        clearLicenseRenewalPending(tenant.tenantId);
      }
      setShowRecoveryBanner(false);
      return;
    }
    setShowRecoveryBanner(isLicenseRenewalPending(tenant.tenantId));
  }, [
    canExtend,
    tenant.tenantId,
    tenant.isRealTenantSlug,
    tenant.isSuperAdminPlatformMode,
    tenant.suppressLicenseWarnings,
    modalOpen,
    status?.state,
  ]);

  if (!showRecoveryBanner || !tenant.tenantId) return null;

  const continueRenewal = () => {
    setShowRecoveryBanner(false);
    openLicenseRenewalModal();
  };

  const dismiss = () => {
    clearLicenseRenewalPending(tenant.tenantId);
    setShowRecoveryBanner(false);
  };

  return (
    <Alert
      type="warning"
      showIcon
      icon={<KeyOutlined />}
      style={{ marginBottom: 16 }}
      title={t('license.renewalRecovery.title')}
      description={
        <Flex align="center" justify="space-between" gap={12} wrap="wrap">
          <span>{t('license.renewalRecovery.description')}</span>
          <Space wrap>
            <Button size="small" type="primary" onClick={continueRenewal}>
              {t('license.renewalRecovery.continue')}
            </Button>
            <Button size="small" onClick={dismiss}>
              {t('license.renewalRecovery.dismiss')}
            </Button>
          </Space>
        </Flex>
      }
    />
  );
}
