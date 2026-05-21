'use client';

import type { ReactNode } from 'react';
import { Alert } from 'antd';

import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useI18n } from '@/i18n';

export type SuperAdminCredentialsGateProps = {
    children: ReactNode;
    /** When false, render nothing (no fallback alert). */
    showRestrictedHint?: boolean;
};

/**
 * Renders children only for SuperAdmin (one-time passwords, tenant user create).
 */
export function SuperAdminCredentialsGate({
    children,
    showRestrictedHint = true,
}: SuperAdminCredentialsGateProps) {
    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();

    if (canProvisionTenantCredentials) {
        return <>{children}</>;
    }

    if (!showRestrictedHint) {
        return null;
    }

    return (
        <Alert
            type="info"
            showIcon
            message={t('tenants.users.superAdminOnly.credentialsTitle')}
            description={t('tenants.users.superAdminOnly.credentialsHint')}
        />
    );
}
