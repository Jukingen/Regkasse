'use client';

import { Alert, Button } from 'antd';

import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useImpersonationExpiryWarning } from '@/features/tenancy/hooks/useImpersonationExpiryWarning';
import { useI18n } from '@/i18n';
import { exitImpersonation } from '@/lib/auth/exitImpersonation';

export function ImpersonationBanner() {
    const { t } = useI18n();
    const { isImpersonating, tenantSlug, tenantName, displayLabel, hasAuthToken } = useCurrentTenant();
    const showImpersonation = isImpersonating && hasAuthToken;
    useImpersonationExpiryWarning(showImpersonation);

    if (!showImpersonation) {
        return null;
    }

    const slug = tenantSlug?.trim() || '—';
    const name = tenantName?.trim() || displayLabel?.trim() || slug;

    return (
        <Alert
            type="info"
            showIcon
            banner
            role="status"
            style={{ marginBottom: 12 }}
            message={t('adminShell.impersonation.banner.title')}
            description={t('adminShell.impersonation.banner.description', { name, slug })}
            action={
                <Button size="small" onClick={() => exitImpersonation()}>
                    {t('adminShell.impersonation.banner.exit')}
                </Button>
            }
        />
    );
}
