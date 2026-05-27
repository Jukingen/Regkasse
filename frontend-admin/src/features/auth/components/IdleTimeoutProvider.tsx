'use client';

import { useCallback, useMemo, useState, type ReactNode } from 'react';

import { SessionTimeoutWarning } from '@/components/SessionTimeoutWarning';
import { useIdleTimeout } from '@/hooks/useIdleTimeout';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { sendSessionHeartbeat } from '@/features/auth/api/sessionsApi';
import type { MeResponse } from '@/features/auth/utils/mapMeResponseToAuthUser';

type SessionPolicy = {
    sessionTimeoutMinutes: number;
    warningBeforeTimeoutMinutes: number;
};

const DEFAULT_POLICY: SessionPolicy = {
    sessionTimeoutMinutes: 30,
    warningBeforeTimeoutMinutes: 1,
};

function readPolicyFromUser(user: unknown): SessionPolicy {
    const u = user as MeResponse & {
        sessionPolicy?: SessionPolicy;
    };
    const p = u.sessionPolicy;
    if (!p) return DEFAULT_POLICY;
    return {
        sessionTimeoutMinutes: p.sessionTimeoutMinutes ?? DEFAULT_POLICY.sessionTimeoutMinutes,
        warningBeforeTimeoutMinutes:
            p.warningBeforeTimeoutMinutes ?? DEFAULT_POLICY.warningBeforeTimeoutMinutes,
    };
}

type Props = {
    children: ReactNode;
};

export function IdleTimeoutProvider({ children }: Props) {
    const { authStatus, logout, user } = useAuth();
    const [warningOpen, setWarningOpen] = useState(false);

    const policy = useMemo(() => readPolicyFromUser(user), [user]);
    const warningSeconds = Math.max(1, policy.warningBeforeTimeoutMinutes * 60);

    const handleTimeout = useCallback(() => {
        setWarningOpen(false);
        void logout();
    }, [logout]);

    const { reset } = useIdleTimeout({
        timeoutMinutes: policy.sessionTimeoutMinutes,
        warningBeforeMinutes: policy.warningBeforeTimeoutMinutes,
        onWarning: () => setWarningOpen(true),
        onTimeout: handleTimeout,
        enabled: authStatus === AuthStatus.Authenticated,
    });

    const handleContinueSession = useCallback(() => {
        setWarningOpen(false);
        reset();
        void sendSessionHeartbeat().catch(() => {
            /* best effort */
        });
    }, [reset]);

    return (
        <>
            {children}
            <SessionTimeoutWarning
                open={warningOpen}
                warningSeconds={warningSeconds}
                onContinueSession={handleContinueSession}
                onCountdownComplete={handleTimeout}
            />
        </>
    );
}
