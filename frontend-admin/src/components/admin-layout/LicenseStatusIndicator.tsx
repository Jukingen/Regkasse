'use client';

/**
 * Header badge shows the most critical active tenant or deployment license phase.
 */

import { LoadingOutlined, WarningOutlined } from '@ant-design/icons';

import {
    useDeploymentLicenseStatus,
    useTenantLicenseStatus,
    type LicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export type LicenseStatusIndicatorProps = {
    compact?: boolean;
};

type CriticalLicenseSource = 'tenant' | 'deployment';
type CriticalLicenseStatus = {
    status: LicenseStatus;
    source: CriticalLicenseSource;
};

function getCriticalOrder(kind: LicenseStatus['kind']): number {
    switch (kind) {
        case 'lockdown':
            return 0;
        case 'grace_readonly':
            return 1;
        case 'grace_write':
            return 2;
        case 'no_license':
        case 'expired':
            return 3;
        case 'active':
        default:
            return 4;
    }
}

function getStatusClass(kind: LicenseStatus['kind']): 'valid' | 'warning' | 'expired' {
    switch (kind) {
        case 'active':
            return 'valid';
        case 'grace_write':
            return 'warning';
        case 'grace_readonly':
        case 'lockdown':
        case 'expired':
        case 'no_license':
        default:
            return 'expired';
    }
}

function getStatusText(entry: CriticalLicenseStatus): string {
    const { status, source } = entry;

    switch (status.kind) {
        case 'grace_write':
            return `${Math.max(0, (source === 'tenant' ? 30 : 15) - status.daysExpired)} Tage`;
        case 'grace_readonly':
            return source === 'tenant' ? 'Verkaeufe deaktiviert' : 'Nur Lesen';
        case 'lockdown':
            return 'System gesperrt';
        case 'no_license':
            return 'Keine Lizenz';
        case 'expired':
            return 'Abgelaufen';
        case 'active':
        default:
            return '';
    }
}

export function LicenseStatusIndicator({ compact: _compact = false }: LicenseStatusIndicatorProps) {
    const { t } = useI18n();
    const tenant = useCurrentTenant();
    const tenantLicenseQuery = useTenantLicenseStatus(
        tenant.isRealTenantSlug ? tenant.tenantId ?? undefined : undefined,
    );
    const deploymentLicenseQuery = useDeploymentLicenseStatus();
    const isLoading =
        tenantLicenseQuery.isLoading ||
        deploymentLicenseQuery.isLoading;

    if (isLoading) {
        return (
            <div className="license-badge loading" aria-busy="true" aria-live="polite">
                <LoadingOutlined className="license-icon" spin aria-hidden />
                <span className="license-text">{t('license.badge.loading')}</span>
            </div>
        );
    }

    const candidates: CriticalLicenseStatus[] = [];
    if (tenantLicenseQuery.data && tenant.isRealTenantSlug) {
        candidates.push({ status: tenantLicenseQuery.data, source: 'tenant' });
    }
    if (deploymentLicenseQuery.data) {
        candidates.push({ status: deploymentLicenseQuery.data, source: 'deployment' });
    }

    const criticalStatus = candidates
        .sort((a, b) => getCriticalOrder(a.status.kind) - getCriticalOrder(b.status.kind))[0];

    if (!criticalStatus || criticalStatus.status.kind === 'active') {
        return null;
    }

    const statusClass = getStatusClass(criticalStatus.status.kind);
    const statusText = getStatusText(criticalStatus);

    return (
        <div className={`license-badge ${statusClass}`} aria-label={statusText}>
            <WarningOutlined className="license-icon" aria-hidden />
            <span className="license-text">{statusText}</span>
        </div>
    );
}

/** @deprecated Use `LicenseStatusIndicator` */
export const LicenseStatusBadge = LicenseStatusIndicator;
