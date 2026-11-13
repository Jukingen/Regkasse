'use client';

/**
 * Locked / Archived mandant license notification (restricted-mode banner).
 * Mounted via {@link LicenseStatusBanner} → {@link LicenseExpiryBanner} on protected layout.
 */
import { LockOutlined } from '@ant-design/icons';
import { Alert, Button, Flex, Space, Typography } from 'antd';
import { useRouter } from 'next/navigation';

import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { redirectToLicensePayment } from '@/features/license/utils/licensePaymentRedirect';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import {
  useLicenseStatus,
  type LicenseStatusView,
} from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { isLicenseLockdownSidebarActive } from '@/shared/sidebarLicenseLockdown';

const { Text, Paragraph } = Typography;

export type LicenseLockdownBannerProps = {
  /** Optional preloaded status (avoids a second fetch when composed from LicenseStatusBanner). */
  status?: LicenseStatusView | null;
};

/**
 * User-facing notification when mandant license is Locked or Archived.
 * Opens the shared renewal modal (or payment redirect) — does not use hardcoded German strings.
 */
export function LicenseLockdownBanner({ status: statusProp }: LicenseLockdownBannerProps = {}) {
  const { t } = useI18n();
  const router = useRouter();
  const tenant = useCurrentTenant();
  const { status: statusFromHook, isLoading } = useLicenseStatus();
  const status = statusProp ?? statusFromHook;
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });

  if (statusProp == null && isLoading) return null;
  if (!status || !isLicenseLockdownSidebarActive(status.state)) return null;
  if (tenant.suppressLicenseWarnings || !tenant.isRealTenantSlug) return null;

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

  const openLicensePage = () => {
    router.push('/license');
  };

  const openDataExport = () => {
    if (tenant.tenantId) {
      router.push(`/tenant/${tenant.tenantId}/data-management`);
      return;
    }
    router.push('/settings/data-management');
  };

  const openAccountManagement = () => {
    router.push('/settings/account');
  };

  const title =
    status.state === 'Archived'
      ? t('license.statusBanner.archived.title')
      : status.anyActive && !status.allActive
        ? t('license.management.systemActiveTenantLocked')
        : t('license.statusBanner.locked.title');

  return (
    <Alert
      type="error"
      banner
      showIcon
      icon={<LockOutlined />}
      role="alert"
      aria-live="assertive"
      style={{ marginBottom: 12 }}
      title={title}
      description={
        <Flex vertical gap={8}>
          <Paragraph style={{ margin: 0 }}>
            {t('license.statusBanner.locked.description', {
              days: status.daysOverdue,
            })}
          </Paragraph>
          <ul style={{ margin: 0, paddingInlineStart: 20 }}>
            <li>
              <Text type="secondary">{t('license.statusBanner.locked.bulletReadOnly')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('license.statusBanner.locked.bulletNoWrite')}</Text>
            </li>
            <li>
              <Text type="secondary">{t('license.statusBanner.locked.bulletRenew')}</Text>
            </li>
          </ul>
          <Space wrap size="middle">
            <Button type="primary" danger size="large" onClick={openRenewal}>
              {t('license.statusBanner.actions.renew')}
            </Button>
            <Button size="large" onClick={openLicensePage}>
              {t('license.statusBanner.actions.openLicensePage')}
            </Button>
            <Button size="large" onClick={openDataExport}>
              {t('license.statusBanner.actions.dataExport')}
            </Button>
            <Button size="large" onClick={openAccountManagement}>
              {t('license.statusBanner.actions.accountManagement')}
            </Button>
          </Space>
        </Flex>
      }
    />
  );
}
