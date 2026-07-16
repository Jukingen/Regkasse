'use client';

import { useEffect } from 'react';
import { Alert } from 'antd';

import { useI18n } from '@/i18n';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { LicenseTestPanel } from '@/features/license/components/LicenseTestPanel';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { NotFoundAccessView } from '@/shared/auth/NotFoundAccessView';
import { technicalConsole } from '@/shared/dev/technicalConsole';

export default function LicenseTestPage() {
    const { t } = useI18n();
    const { tenant } = useTenant();

    useEffect(() => {
        if (!isDevelopment()) {
            return;
        }
        technicalConsole.devLog('[License Test] page tenant:', tenant);
        technicalConsole.devLog('[License Test] page tenantId:', tenant?.id ?? null);
    }, [tenant]);

    if (!isDevelopment()) {
        return (
            <div style={{ padding: 24 }}>
                <Alert type="warning" message={t('license.testPanel.productionBlocked')} showIcon />
                <NotFoundAccessView compact />
            </div>
        );
    }

    return (
        <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 16 }}>
            <Alert
                message={t('license.testPanel.alertTitle')}
                description={t('license.testPanel.alertDescription')}
                type="warning"
                showIcon
            />
            <Alert type="info" message={t('license.testPanel.overrideHint')} showIcon />
            <LicenseTestPanel />
        </div>
    );
}
