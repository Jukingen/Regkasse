'use client';

import { useEffect, useRef, ReactNode, FC } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { CHANGE_PASSWORD_PATH, isChangePasswordPath } from '@/features/auth/constants/changePasswordRoute';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { Spin } from 'antd';
import { useI18n } from '@/i18n';

interface GuardProps {
    children: ReactNode;
    mode: 'protected' | 'public';
}

/** Public auth screens that render immediately without the global session spinner. */
function isPublicAuthEntryPath(pathname: string): boolean {
    return (
        pathname === '/login' ||
        pathname.startsWith('/login/') ||
        isChangePasswordPath(pathname) ||
        pathname === '/impersonate-callback'
    );
}

/**
 * Centralized auth gate for public and protected route groups.
 * Enforces login, mandatory password change, and post-auth public redirects.
 */
export const AuthGate: FC<GuardProps> = ({ children, mode }) => {
    const { t } = useI18n();
    const { isAuthenticated, isLoading, mustChangePassword } = useAuth();
    const router = useRouter();
    const pathname = usePathname();
    const alreadyRedirected = useRef<string | null>(null);

    const isPasswordChangePage = isChangePasswordPath(pathname);
    const passwordChangeRequired = mustChangePassword === true;
    const skipSessionSpinner = mode === 'public' && isPublicAuthEntryPath(pathname);

    useEffect(() => {
        if (isLoading) {
            return;
        }

        if (mode === 'protected') {
            if (!isAuthenticated) {
                if (pathname !== '/login' && alreadyRedirected.current !== '/login') {
                    technicalConsole.devLog(`[AuthGate] protected redirect: ${pathname} -> /login`);
                    alreadyRedirected.current = '/login';
                    router.replace('/login');
                }
                return;
            }

            if (passwordChangeRequired && !isPasswordChangePage) {
                if (alreadyRedirected.current !== CHANGE_PASSWORD_PATH) {
                    technicalConsole.devLog(`[AuthGate] protected redirect: ${pathname} -> ${CHANGE_PASSWORD_PATH}`);
                    alreadyRedirected.current = CHANGE_PASSWORD_PATH;
                    router.replace(CHANGE_PASSWORD_PATH);
                }
            }
            return;
        }

        if (pathname === '/impersonate-callback') {
            return;
        }

        if (!isAuthenticated) {
            if (isPasswordChangePage && alreadyRedirected.current !== '/login') {
                alreadyRedirected.current = '/login';
                router.replace('/login');
            }
            return;
        }

        if (isPasswordChangePage) {
            if (!passwordChangeRequired && alreadyRedirected.current !== '/dashboard') {
                alreadyRedirected.current = '/dashboard';
                router.replace('/dashboard');
            }
            return;
        }

        const target = passwordChangeRequired ? CHANGE_PASSWORD_PATH : '/dashboard';
        if (pathname !== target && alreadyRedirected.current !== target) {
            technicalConsole.devLog(`[AuthGate] public redirect: ${pathname} -> ${target}`);
            alreadyRedirected.current = target;
            router.replace(target);
        }
    }, [isLoading, isAuthenticated, passwordChangeRequired, isPasswordChangePage, mode, pathname, router]);

    if (skipSessionSpinner) {
        return <>{children}</>;
    }

    if (isLoading) {
        return (
            <div
                style={{
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    height: '100vh',
                    background: '#f0f2f5',
                }}
            >
                <Spin spinning description={t('common.auth.checkingSession')}>
                    <div style={{ padding: 50 }} />
                </Spin>
            </div>
        );
    }

    if (mode === 'protected') {
        if (!isAuthenticated) {
            return null;
        }
        if (passwordChangeRequired) {
            return null;
        }
        return <>{children}</>;
    }

    if (!isAuthenticated && isPasswordChangePage) {
        return null;
    }

    if (isAuthenticated && pathname !== '/impersonate-callback' && !isPasswordChangePage) {
        return null;
    }

    return <>{children}</>;
};
