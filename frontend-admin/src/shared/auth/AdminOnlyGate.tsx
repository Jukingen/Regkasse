'use client';

import { useEffect, ReactNode, FC } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { Spin } from 'antd';

interface AdminOnlyGateProps {
    children: ReactNode;
}

/**
 * Admin-only erişim: Administrator rolü yoksa /403'e yönlendirir.
 */
export const AdminOnlyGate: FC<AdminOnlyGateProps> = ({ children }) => {
    const { user, authStatus, isInitialized } = useAuth();
    const router = useRouter();
    const pathname = usePathname();

    const isAdmin = user?.role === 'Administrator';

    useEffect(() => {
        if (!isInitialized || authStatus === AuthStatus.Loading) return;
        if (authStatus === AuthStatus.Unauthenticated) return;
        if (!isAdmin) {
            router.replace('/403');
        }
    }, [isInitialized, authStatus, isAdmin, router, pathname]);

    if (!isInitialized || authStatus === AuthStatus.Loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 80 }}>
                <Spin size="large" tip="Checking authorization..." />
            </div>
        );
    }

    if (authStatus === AuthStatus.Unauthenticated) return null;
    if (!isAdmin) return null;

    return <>{children}</>;
};
