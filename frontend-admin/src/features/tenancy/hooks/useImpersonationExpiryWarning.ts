'use client';

import { message } from 'antd';
import { useEffect, useRef } from 'react';

import { useImpersonationTokenExpiry } from '@/features/tenancy/hooks/useImpersonationTokenExpiry';
import { useI18n } from '@/i18n';

const EXPIRY_MESSAGE_KEY = 'impersonation-token-expiry';

/**
 * Shows an Ant Design warning toast when the impersonation JWT `exp` is under 5 minutes away.
 */
export function useImpersonationExpiryWarning(enabled: boolean): void {
    const { t } = useI18n();
    const { minutesRemaining, shouldWarn } = useImpersonationTokenExpiry(enabled);
    const lastToastMinutes = useRef<number | null>(null);

    useEffect(() => {
        if (!enabled || !shouldWarn || minutesRemaining == null) {
            lastToastMinutes.current = null;
            return;
        }

        if (lastToastMinutes.current === minutesRemaining) {
            return;
        }

        lastToastMinutes.current = minutesRemaining;
        message.warning({
            key: EXPIRY_MESSAGE_KEY,
            content: t('adminShell.impersonation.banner.expiryToast', { minutes: minutesRemaining }),
            duration: 8,
        });
    }, [enabled, shouldWarn, minutesRemaining, t]);
}
