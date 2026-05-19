'use client';

import { Space, Spin } from 'antd';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { SuperAdminTenantSelector } from '@/features/super-admin/components/SuperAdminTenantSelector';
import { TenantInfoCard } from '@/features/tenant/components/TenantInfoCard';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';

/**
 * Platform admin landing (`/admin`). Super Admin on `admin.*` without mandant → tenant hub;
 * with impersonation / dev override → operational dashboard.
 */
export default function AdminPlatformPage() {
    const router = useRouter();
    const { user, isLoadingAuth: authLoading } = useAuth();
    const { requiresTenantSelection, hasAuthToken, isSuperAdminUser, isPlatformAdminHost } =
        useSuperAdminTenantMode();

    useEffect(() => {
        if (!hasAuthToken || authLoading) {
            return;
        }
        if (!isSuperAdmin(user?.role)) {
            router.replace('/dashboard');
            return;
        }
        if (!requiresTenantSelection) {
            router.replace('/dashboard');
        }
    }, [authLoading, hasAuthToken, isSuperAdminUser, requiresTenantSelection, router, user?.role]);

    if (authLoading || !hasAuthToken) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
                <Spin />
            </div>
        );
    }

    if (!isSuperAdmin(user?.role) || !isPlatformAdminHost) {
        return null;
    }

    if (!requiresTenantSelection) {
        return (
            <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <TenantInfoCard />
                <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
                    <Spin />
                </div>
            </Space>
        );
    }

    return (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <TenantInfoCard />
            <SuperAdminTenantSelector />
        </Space>
    );
}
