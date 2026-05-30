'use client';

import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { connectActivityStream } from '@/api/manual/activityEvents';
import {
    fetchSuspiciousAlerts,
    markSuspiciousAlertRead,
} from '@/features/alerts/api/suspiciousAlerts';
import type { SuspiciousAlert } from '@/features/alerts/types';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const alertsKey = ['admin', 'payments', 'suspicious-alerts'] as const;
const POLL_MS = 30_000;

function isSuspiciousActivityType(type: string): boolean {
    return type.startsWith('Suspicious');
}

export function useSuspiciousAlertsAccess(): boolean {
    const { hasPermission } = usePermissions();
    return hasPermission(PERMISSIONS.PAYMENT_VIEW);
}

export function useSuspiciousAlerts(options?: { unreadOnly?: boolean; enabled?: boolean }) {
    const unreadOnly = options?.unreadOnly ?? true;
    const enabled = options?.enabled ?? true;

    const query = useQuery({
        queryKey: [...alertsKey, { unreadOnly }],
        queryFn: ({ signal }) => fetchSuspiciousAlerts({ unreadOnly }, signal),
        enabled,
        refetchInterval: enabled ? POLL_MS : false,
        refetchOnWindowFocus: enabled,
        select: (response): SuspiciousAlert[] => response.items,
    });

    useSuspiciousAlertsActivityRefresh(enabled);

    return query;
}

/** Refreshes alert list when suspicious events arrive on the activity SSE stream. */
function useSuspiciousAlertsActivityRefresh(enabled: boolean) {
    const queryClient = useQueryClient();
    const canStream = useActivityStreamAccess();

    useEffect(() => {
        if (!enabled || !canStream) {
            return;
        }

        const abort = new AbortController();

        void connectActivityStream(
            {
                onActivity: (activity) => {
                    if (!isSuspiciousActivityType(activity.type)) {
                        return;
                    }
                    void queryClient.invalidateQueries({ queryKey: alertsKey });
                },
            },
            abort.signal,
        ).catch(() => {
            // Non-fatal; polling remains active.
        });

        return () => abort.abort();
    }, [canStream, enabled, queryClient]);
}

function useActivityStreamAccess(): boolean {
    const { hasPermission } = usePermissions();
    return hasPermission(PERMISSIONS.SETTINGS_VIEW);
}

export function useMarkAlertAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (alertId: string) => markSuspiciousAlertRead(alertId),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: alertsKey });
        },
    });
}
