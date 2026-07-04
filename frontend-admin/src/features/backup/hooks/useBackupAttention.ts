'use client';

import { useQuery } from '@tanstack/react-query';

import {
    BACKUP_DASHBOARD_STATS_POLL_MS,
    getBackupDashboardStats,
    getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import { resolveBackupRunStatusUiKey } from '@/features/backup/logic/backupRunTablePresentation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

function isBackupFailureStatus(status: number | undefined): boolean {
    const uiKey = resolveBackupRunStatusUiKey(status);
    return uiKey === 'failed' || uiKey === 'verificationFailed';
}

/**
 * Surfaces when the tenant's latest backup run needs operator attention (failed / verification failed).
 */
export function useBackupAttention() {
    const { user } = useAuth();
    const canView = hasPermission(user, PERMISSIONS.SETTINGS_VIEW);

    const query = useQuery({
        queryKey: getBackupDashboardStatsQueryKey(),
        queryFn: getBackupDashboardStats,
        enabled: canView,
        staleTime: BACKUP_DASHBOARD_STATS_POLL_MS,
        refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
        refetchOnWindowFocus: true,
    });

    const needsAttention = isBackupFailureStatus(query.data?.lastBackupStatus);

    return {
        needsAttention,
        lastBackupStatus: query.data?.lastBackupStatus,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
    };
}
