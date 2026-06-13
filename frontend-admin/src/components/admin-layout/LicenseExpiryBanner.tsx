'use client';

/**
 * Tenant-first license warning surface for protected admin pages.
 * SuperAdmin: actionable configuration warnings. Other roles: operational grace only.
 */

import type { ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { Alert, Button, Space } from 'antd';

import {
    useDeploymentLicenseStatus,
    useTenantLicenseStatus,
    type LicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { useI18n } from '@/i18n';

function isAdminConfigurationLicenseIssue(kind: LicenseStatus['kind']): boolean {
    return kind === 'no_license' || kind === 'lockdown';
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

    const openTenantLicensePage = () => {
        if (!tenant.tenantId) {
            return;
        }
        router.push(`/admin/tenants/${tenant.tenantId}?tab=license`);
    };

    const openDeploymentLicensePage = () => {
        router.push('/admin/license');
    };

    const renderTenantRenewAction = () =>
        tenant.isSuperAdminUser && tenant.tenantId ? (
            <Button size="small" type="primary" onClick={openTenantLicensePage}>
                {t('license.banner.actions.renewTenant')}
            </Button>
        ) : null;

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

    const renderTenantBanner = (license: LicenseStatus) => {
        if (!tenant.isSuperAdminUser && isAdminConfigurationLicenseIssue(license.kind)) {
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
                        title={t('license.banner.tenant.graceWrite.title')}
                        description={renderBannerDescription(
                            tenant.isSuperAdminUser
                                ? t('license.banner.tenant.graceWrite.adminDescription', {
                                      daysExpired: license.daysExpired,
                                      daysRemaining: Math.max(
                                          0,
                                          TENANT_GRACE_PERIOD_DAYS - license.daysExpired,
                                      ),
                                  })
                                : t('license.banner.tenant.graceWrite.contactAdminDescription', {
                                      daysExpired: license.daysExpired,
                                  }),
                            tenant.isSuperAdminUser ? renderTenantRenewAction() : null,
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
                        title={t('license.banner.tenant.lockdown.title')}
                        description={renderBannerDescription(
                            tenant.isSuperAdminUser
                                ? t('license.banner.tenant.lockdown.adminDescription', {
                                      daysExpired: license.daysExpired,
                                  })
                                : t('license.banner.tenant.lockdown.contactAdminDescription'),
                            tenant.isSuperAdminUser ? renderTenantRenewAction() : null,
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
                        title={t('license.banner.tenant.noLicense.title')}
                        description={renderBannerDescription(
                            t('license.banner.tenant.noLicense.adminDescription'),
                            renderTenantRenewAction(),
                        )}
                    />
                );
            default:
                return null;
        }
    };

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
                                daysRemaining: Math.max(0, 15 - license.daysExpired),
                            }),
                            renderDeploymentRenewAction(),
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
                            renderDeploymentRenewAction(),
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
                            renderDeploymentRenewAction(),
                        )}
                    />
                );
            default:
                return null;
        }
    };

    if (tenantLicense && tenantLicense.kind !== 'active' && tenant.isRealTenantSlug) {
        const banner = renderTenantBanner(tenantLicense);
        if (banner) {
            return banner;
        }
    }

    if (deploymentLicense && deploymentLicense.kind !== 'active') {
        const banner = renderDeploymentBanner(deploymentLicense);
        if (banner) {
            return banner;
        }
    }

    return null;
}
