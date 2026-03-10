'use client';

import React, { useEffect, ReactNode, FC } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth, AuthStatus } from '@/features/auth/hooks/useAuth';
import { hasAnyPermission } from './permissions';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { Spin } from 'antd';

const ADMIN_PERMISSIONS = ['user.manage', 'settings.manage'] as const;

interface AdminOnlyGateProps {
    children: ReactNode;
}

/**
 * Admin-only access: permission-first (user.manage or settings.manage); fallback SuperAdmin (legacy Admin token treated as SuperAdmin).
 * Redirects to /403 if user lacks admin permission/role. No legacy role names.
 */
export const AdminOnlyGate: FC<AdminOnlyGateProps> = ({ children }) => {
    const { user, authStatus, isInitialized } = useAuth();
    const router = useRouter();
    const pathname = usePathname();
    const permissions = user?.permissions ?? [];
    const role = user?.role ?? '';
    const isAdmin =
        permissions.length > 0
            ? hasAnyPermission(user, [...ADMIN_PERMISSIONS])
            : isSuperAdmin(role);

    useEffect(() => {
        if (!isInitialized || authStatus === AuthStatus.Loading) return;
        if (authStatus === AuthStatus.Unauthenticated) return;
        if (!isAdmin) router.replace('/403');
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
