'use client';

/**
 * Tenant-first license warning surface for protected admin pages.
 * Mandant grace/lock → {@link LicenseBanner}; SuperAdmin also sees deployment license alerts.
 */
import { Alert, Button, Space } from 'antd';
import { useRouter } from 'next/navigation';
import type { ReactNode } from 'react';

import { LicenseBanner } from '@/components/LicenseBanner';
import {
  type LicenseStatus,
  useDeploymentLicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

function hasMandantBanner(license: LicenseStatus | undefined, isSuperAdmin: boolean): boolean {
  if (!license || license.kind === 'active') return false;
  if (!isSuperAdmin && license.kind === 'no_license') {
    return false;
  }
  return (
    license.kind === 'grace_write' || license.kind === 'lockdown' || license.kind === 'no_license'
  );
}

export function LicenseExpiryBanner() {
  const router = useRouter();
  const { t } = useI18n();
  const tenant = useCurrentTenant();
  const { data: tenantLicense } = useTenantLicenseStatus();
  const deploymentStatusQuery = useDeploymentLicenseStatus();
  const deploymentLicense = deploymentStatusQuery.data;

  if (tenant.suppressLicenseWarnings) {
    return null;
  }

  const showMandant =
    tenant.isRealTenantSlug && hasMandantBanner(tenantLicense, tenant.isSuperAdminUser);

  const openDeploymentLicensePage = () => {
    router.push('/admin/license');
  };

  const renderDeploymentRenewAction = () =>
    tenant.isSuperAdminUser ? (
      <Button size="small" type="primary" onClick={openDeploymentLicensePage}>
        {t('license.banner.actions.openDeployment')}
      </Button>
    ) : null;

  const renderBannerDescription = (content: ReactNode, action?: ReactNode) => (
    <Space orientation="vertical" size="small">
      <span>{content}</span>
      {action ?? null}
    </Space>
  );

  const renderDeploymentBanner = (license: LicenseStatus) => {
    if (!tenant.isSuperAdminUser) {
      return null;
    }

    switch (license.kind) {
      case 'grace_write':
        return (
          <Alert
            type="warning"
            banner
            showIcon
            style={{ marginBottom: 12 }}
            title={t('license.banner.deployment.graceWrite.title')}
            description={renderBannerDescription(
              t('license.banner.deployment.graceWrite.adminDescription', {
                daysExpired: license.daysExpired,
                daysRemaining: license.daysRemainingInGrace,
              }),
              renderDeploymentRenewAction()
            )}
          />
        );
      case 'grace_readonly':
        return null;
      case 'lockdown':
        return (
          <Alert
            type="error"
            banner
            showIcon
            style={{ marginBottom: 12 }}
            title={t('license.banner.deployment.lockdown.title')}
            description={renderBannerDescription(
              t('license.banner.deployment.lockdown.adminDescription', {
                daysExpired: license.daysExpired,
              }),
              renderDeploymentRenewAction()
            )}
          />
        );
      case 'no_license':
        return (
          <Alert
            type="error"
            banner
            showIcon
            style={{ marginBottom: 12 }}
            title={t('license.banner.deployment.noLicense.title')}
            description={renderBannerDescription(
              t('license.banner.deployment.noLicense.adminDescription'),
              renderDeploymentRenewAction()
            )}
          />
        );
      default:
        return null;
    }
  };

  const deploymentBanner =
    !showMandant &&
    deploymentLicense &&
    deploymentLicense.kind !== 'active' &&
    tenant.isSuperAdminUser
      ? renderDeploymentBanner(deploymentLicense)
      : null;

  return (
    <>
      {showMandant ? <LicenseBanner /> : null}
      {deploymentBanner}
    </>
  );
}
