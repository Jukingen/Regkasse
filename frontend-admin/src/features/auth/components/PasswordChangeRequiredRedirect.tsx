'use client';

import { useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { Alert } from 'antd';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useI18n } from '@/i18n';

/**
 * Redirects users who must change their password to the settings password tab.
 */
export function PasswordChangeRequiredRedirect() {
    const { user } = useAuth();
    const pathname = usePathname();
    const router = useRouter();
    const { t } = useI18n();

    const mustChange = user?.mustChangePasswordOnNextLogin === true;
    const onSettings = pathname?.startsWith('/settings');

    useEffect(() => {
        if (mustChange && !onSettings) {
            router.replace('/settings?mustChangePassword=1');
        }
    }, [mustChange, onSettings, router]);

    if (!mustChange || onSettings) {
        return null;
    }

    return (
        <Alert
            type="warning"
            showIcon
            message={t('settings.changePassword.requiredBanner')}
            style={{ margin: '0 16px 16px' }}
        />
    );
}
