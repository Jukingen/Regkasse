'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
    fetchSessionSettings,
    updateSessionSettings,
    type SessionSettings,
    type UpdateSessionSettingsPayload,
} from '@/features/settings/api/sessionSettingsApi';
import { AUTH_KEYS } from '@/features/auth/hooks/useAuth';

export const SESSION_SETTINGS_KEYS = {
    all: ['session-settings'] as const,
};

export function useSessionSettings() {
    return useQuery({
        queryKey: SESSION_SETTINGS_KEYS.all,
        queryFn: fetchSessionSettings,
        staleTime: 60_000,
    });
}

export function useUpdateSessionSettings() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (payload: UpdateSessionSettingsPayload) => updateSessionSettings(payload),
        onSuccess: (data: SessionSettings) => {
            queryClient.setQueryData(SESSION_SETTINGS_KEYS.all, data);
            void queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user });
        },
    });
}
