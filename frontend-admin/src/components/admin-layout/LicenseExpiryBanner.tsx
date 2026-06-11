'use client';

/**
 * Tenant-first license warning surface for protected admin pages.
 * Login remains available; the banner explains degraded write access only.
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

export function LicenseExpiryBanner() {
    const router = useRouter();
    const tenant = useCurrentTenant();
    const { data: tenantLicense } = useTenantLicenseStatus();
    const deploymentStatusQuery = useDeploymentLicenseStatus();
    const deploymentLicense = deploymentStatusQuery.data;

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
                Lizenz jetzt verlaengern
            </Button>
        ) : null;

    const renderDeploymentRenewAction = () =>
        tenant.isSuperAdminUser ? (
            <Button size="small" type="primary" onClick={openDeploymentLicensePage}>
                Server-Lizenz oeffnen
            </Button>
        ) : null;

    const renderBannerDescription = (content: ReactNode, action?: ReactNode) => (
        <Space orientation="vertical" size="small">
            <span>{content}</span>
            {action ?? null}
        </Space>
    );

    const renderTenantBanner = (license: LicenseStatus) => {
        switch (license.kind) {
            case 'grace_write':
                return (
                    <Alert
                        type="warning"
                        banner
                        showIcon
                        style={{ marginBottom: 12 }}
                        title="Mandantenlizenz - Grace-Periode"
                        description={renderBannerDescription(
                            <>
                                Ihre Lizenz ist seit <strong>{license.daysExpired}</strong> Tagen abgelaufen. Sie
                                haben noch <strong>{Math.max(0, TENANT_GRACE_PERIOD_DAYS - license.daysExpired)}</strong> Tage Zeit, um
                                die Lizenz zu verlaengern. Danach wird der Mandant gesperrt.
                            </>,
                            renderTenantRenewAction(),
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
                        title="Mandantenlizenz abgelaufen - System gesperrt"
                        description={renderBannerDescription(
                            <>
                                Ihre Lizenz ist seit <strong>{license.daysExpired}</strong> Tagen abgelaufen. Das
                                System ist jetzt im Lockdown-Modus. Bitte kontaktieren Sie Ihren Administrator.
                            </>,
                            renderTenantRenewAction(),
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
                        title="Mandantenlizenz fehlt"
                        description={renderBannerDescription(
                            <>Fuer diesen Mandanten ist keine Lizenz hinterlegt. Bitte hinterlegen Sie eine Lizenz.</>,
                            renderTenantRenewAction(),
                        )}
                    />
                );
            default:
                return null;
        }
    };

    const renderDeploymentBanner = (license: LicenseStatus) => {
        switch (license.kind) {
            case 'grace_write':
                return (
                    <Alert
                        type="warning"
                        banner
                        showIcon
                        style={{ marginBottom: 12 }}
                        title="Deployment-Lizenz - Grace-Periode"
                        description={renderBannerDescription(
                            <>
                                Die Deployment-Lizenz ist seit <strong>{license.daysExpired}</strong> Tagen
                                abgelaufen. Sie haben noch <strong>{Math.max(0, 15 - license.daysExpired)}</strong>{' '}
                                Tage Zeit, bevor Schreiboperationen eingeschraenkt werden.
                            </>,
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
                        title="Deployment-Lizenz abgelaufen - System im Lockdown"
                        description={renderBannerDescription(
                            <>
                                Die Deployment-Lizenz ist seit <strong>{license.daysExpired}</strong> Tagen
                                abgelaufen. Nur Health- und Lizenz-Aktivierungsendpunkte bleiben verfuegbar.
                            </>,
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
                        title="Deployment-Lizenz fehlt"
                        description={renderBannerDescription(
                            <>Fuer dieses Deployment ist keine Lizenz hinterlegt.</>,
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
