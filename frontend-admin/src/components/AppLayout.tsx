'use client';

import { useCallback, useMemo, type ReactNode } from 'react';
import { message } from 'antd';
import { SessionTimeoutWarning } from '@/components/SessionTimeoutWarning';
import { useSessionTimeout } from '@/hooks/useSessionTimeout';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { refreshAuthSession } from '@/features/auth/api/authSessionApi';
import { useI18n } from '@/i18n';
import type { AuthUser } from '@/shared/auth/types';

const DEFAULT_POLICY = {
    sessionTimeoutMinutes: 30,
    warningBeforeTimeoutMinutes: 1,
    idleTimeoutEnabled: true,
};

function readPolicyFromUser(user: AuthUser | undefined) {
    const p = user?.sessionPolicy;
    if (!p) return DEFAULT_POLICY;
    return {
        sessionTimeoutMinutes: p.sessionTimeoutMinutes ?? DEFAULT_POLICY.sessionTimeoutMinutes,
        warningBeforeTimeoutMinutes:
            p.warningBeforeTimeoutMinutes ?? DEFAULT_POLICY.warningBeforeTimeoutMinutes,
        idleTimeoutEnabled: p.idleTimeoutEnabled ?? DEFAULT_POLICY.idleTimeoutEnabled,
    };
}

type AppLayoutProps = {
    children: ReactNode;
};

/**
 * Protected shell wrapper: idle session timeout warning + auto-logout.
 * Mount inside authenticated routes (see `(protected)/layout.tsx`).
 */
export function AppLayout({ children }: AppLayoutProps) {
    const { t } = useI18n();
    const { authStatus, logout, user } = useAuth();

    const policy = useMemo(() => readPolicyFromUser(user), [user]);
    const warningTotalSeconds = Math.max(1, policy.warningBeforeTimeoutMinutes * 60);

    const { showWarning, secondsRemaining, resetTimers } = useSessionTimeout({
        timeoutMinutes: policy.sessionTimeoutMinutes,
        warningMinutes: policy.warningBeforeTimeoutMinutes,
        enabled:
            authStatus === AuthStatus.Authenticated && policy.idleTimeoutEnabled,
        onTimeout: () => {
            message.warning(t('common.auth.sessionTimeout.loggedOutInactivity'));
        },
    });

    const handleContinue = useCallback(() => {
        resetTimers();
        void refreshAuthSession().catch(() => {
            /* best effort */
        });
    }, [resetTimers]);

    const handleLogout = useCallback(() => {
        void logout();
    }, [logout]);

    return (
        <>
            {children}
            <SessionTimeoutWarning
                open={showWarning}
                secondsRemaining={secondsRemaining}
                warningTotalSeconds={warningTotalSeconds}
                onContinue={handleContinue}
                onLogout={handleLogout}
            />
        </>
    );
}
