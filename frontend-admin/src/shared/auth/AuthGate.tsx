'use client';

import { useEffect, useRef, ReactNode, FC } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { Spin } from 'antd';
import { useI18n } from '@/i18n';

interface GuardProps {
    children: ReactNode;
    mode: 'protected' | 'public';
}

/**
 * Centralized Auth Gate
 * Managed strict redirect logic for public and protected routes.
 */
export const AuthGate: FC<GuardProps> = ({ children, mode }) => {
    const { t } = useI18n();
    const { authStatus, isAuthInitializing } = useAuth();
    const router = useRouter();
    const pathname = usePathname();
    const alreadyRedirected = useRef<string | null>(null);

    useEffect(() => {
        if (isAuthInitializing) return;

        const currentPath = pathname;
        // Protected Mode: authenticated -> children, unauthenticated -> /login
        if (mode === 'protected') {
            if (authStatus === AuthStatus.Unauthenticated) {
                if (alreadyRedirected.current !== '/login' && currentPath !== '/login') {
                    technicalConsole.devLog(`[AuthGate] protected mode redirect: ${currentPath} -> /login`);
                    alreadyRedirected.current = '/login';
                    router.replace('/login');
                }
            }
        }
        // Public Mode: unauthenticated -> children, authenticated -> /dashboard
        else if (mode === 'public') {
            if (currentPath === '/impersonate-callback') {
                return;
            }
            if (authStatus === AuthStatus.Authenticated) {
                if (alreadyRedirected.current !== '/dashboard' && currentPath !== '/dashboard') {
                    technicalConsole.devLog(`[AuthGate] public mode redirect: ${currentPath} -> /dashboard`);
                    alreadyRedirected.current = '/dashboard';
                    router.replace('/dashboard');
                }
            }
        }
    }, [authStatus, isAuthInitializing, router, mode, pathname]);

    useEffect(() => {
        if (process.env.NODE_ENV === 'production') return;
        if (isAuthInitializing) {
            technicalConsole.devLog(`[AuthGate] path=${pathname} mode=${mode} decision=spinner (auth initializing)`);
            return;
        }
        if (mode === 'protected' && authStatus === AuthStatus.Unauthenticated) {
            technicalConsole.devLog(`[AuthGate] path=${pathname} mode=${mode} decision=null+replaceLogin`);
            return;
        }
        if (mode === 'public' && authStatus === AuthStatus.Authenticated) {
            technicalConsole.devLog(`[AuthGate] path=${pathname} mode=${mode} decision=null+replaceDashboard`);
            return;
        }
        technicalConsole.devLog(`[AuthGate] path=${pathname} mode=${mode} decision=renderChildren authStatus=${authStatus}`);
    }, [pathname, mode, isAuthInitializing, authStatus]);

    if (isAuthInitializing) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: '#f0f2f5' }}>
                <Spin spinning tip={t('common.auth.checkingSession')}>
                    <div style={{ padding: 50 }} />
                </Spin>
            </div>
        );
    }

    // Strictly control rendering based on mode and status to prevent flashes
    if (mode === 'protected' && authStatus === AuthStatus.Unauthenticated) return null;
    if (mode === 'public' && authStatus === AuthStatus.Authenticated && pathname !== '/impersonate-callback') {
        return null;
    }

    return <>{children}</>;
};
