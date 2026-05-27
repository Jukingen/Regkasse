'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
    fetchActivities,
    fetchActivityUnreadCount,
    fetchNotificationConfig,
    markActivityRead,
    markAllActivitiesRead,
    saveNotificationConfig,
    type ActivitiesListResponse,
    type NotificationConfig,
} from '@/api/manual/activityEvents';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const unreadKey = ['admin', 'activities', 'unread-count'] as const;
const listKey = ['admin', 'activities', 'list'] as const;
const configKey = ['admin', 'activities', 'notification-config'] as const;

const POLL_MS = 30_000;

export function useActivityUnreadCount(enabled: boolean) {
    return useQuery({
        queryKey: unreadKey,
        queryFn: ({ signal }) => fetchActivityUnreadCount(signal),
        enabled,
        refetchInterval: enabled ? POLL_MS : false,
    });
}

export function useActivitiesList(enabled: boolean) {
    return useQuery({
        queryKey: listKey,
        queryFn: ({ signal }) => fetchActivities({ limit: 50, offset: 0 }, signal),
        enabled,
    });
}

export function useMarkActivityRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => markActivityRead(id),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: unreadKey });
            void queryClient.invalidateQueries({ queryKey: listKey });
        },
    });
}

export function useMarkAllActivitiesRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => markAllActivitiesRead(),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: unreadKey });
            void queryClient.invalidateQueries({ queryKey: listKey });
        },
    });
}

export function useActivityNotificationsAccess() {
    const { hasPermission } = usePermissions();
    return hasPermission(PERMISSIONS.SETTINGS_VIEW);
}

export function useNotificationConfig(enabled: boolean) {
    return useQuery({
        queryKey: configKey,
        queryFn: ({ signal }) => fetchNotificationConfig(signal),
        enabled,
    });
}

export function useSaveNotificationConfig() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (config: NotificationConfig) => saveNotificationConfig(config),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: configKey });
        },
    });
}

export type { ActivitiesListResponse, NotificationConfig };
