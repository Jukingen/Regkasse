'use client';

import { useEffect, useRef, ReactNode, FC } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { Spin } from 'antd';

interface GuardProps {
    children: ReactNode;
    mode: 'protected' | 'public';
}

/**
 * Centralized Auth Gate
 * Managed strict redirect logic for public and protected routes.
 */
export const AuthGate: FC<GuardProps> = ({ children, mode }) => {
    const { authStatus, isInitialized } = useAuth();
    const router = useRouter();
    const pathname = usePathname();
    const alreadyRedirected = useRef<string | null>(null);

    useEffect(() => {
        // Decide only when auth is strictly initialized
        if (!isInitialized || authStatus === AuthStatus.Loading) return;

        const currentPath = pathname;
        const hasToken = typeof window !== 'undefined' && !!localStorage.getItem('rk_admin_access_token');

        // Protected Mode: authenticated -> children, unauthenticated -> /login
        if (mode === 'protected') {
            if (authStatus === AuthStatus.Unauthenticated) {
                if (alreadyRedirected.current !== '/login' && currentPath !== '/login') {
                    console.log(`🔒 [AuthGate] Protected mode: '${currentPath}' -> /login`);
                    alreadyRedirected.current = '/login';
                    router.replace('/login');
                }
            }
        }
        // Public Mode: unauthenticated -> children, authenticated -> /dashboard
        else if (mode === 'public') {
            if (authStatus === AuthStatus.Authenticated) {
                if (alreadyRedirected.current !== '/dashboard' && currentPath !== '/dashboard') {
                    console.log(`🔒 [AuthGate] Public mode: '${currentPath}' -> /dashboard`);
                    alreadyRedirected.current = '/dashboard';
                    router.replace('/dashboard');
                }
            }
        }
    }, [authStatus, isInitialized, router, mode, pathname]);

    // Show spinner while deciding or loading
    if (!isInitialized || authStatus === AuthStatus.Loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: '#f0f2f5' }}>
                <Spin spinning tip="Checking authentication...">
                    <div style={{ padding: 50 }} />
                </Spin>
            </div>
        );
    }

    // Strictly control rendering based on mode and status to prevent flashes
    if (mode === 'protected' && authStatus === AuthStatus.Unauthenticated) return null;
    if (mode === 'public' && authStatus === AuthStatus.Authenticated) return null;

    return <>{children}</>;
};
