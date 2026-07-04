'use client';

import { useEffect, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { usePermissions } from '@/shared/auth/usePermissions';

type Props = {
    children: ReactNode;
    fallback: ReactNode;
};

/**
 * Tenant Managers land on the activity log tab; Super Admin keeps compliance audit as default.
 */
export function ManagerAuditLogsDefaultTab({ children, fallback }: Props) {
    const router = useRouter();
    const { isLoading: authLoading } = useAuth();
    const { isManager } = usePermissions();

    useEffect(() => {
        if (!authLoading && isManager) {
            router.replace('/audit-logs/activity');
        }
    }, [authLoading, isManager, router]);

    if (authLoading || isManager) {
        return fallback;
    }

    return children;
}
