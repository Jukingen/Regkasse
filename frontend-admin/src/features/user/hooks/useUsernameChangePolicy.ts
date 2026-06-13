'use client';

import { useQuery } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';

import { authStorage } from '@/features/auth/services/authStorage';
import { customInstance } from '@/lib/axios';

export const USERNAME_CHANGE_POLICY_QUERY_KEY = ['username-change-policy'] as const;

export type UsernameChangePolicy = {
    cooldownDays: number;
    canChange: boolean;
    restrictionsApply?: boolean;
    lastChangedAtUtc?: string | null;
    nextChangeAllowedAtUtc?: string | null;
};

const emptySubscribe = () => () => {};

async function fetchUsernameChangePolicy(): Promise<UsernameChangePolicy> {
    return customInstance<UsernameChangePolicy>({
        url: '/api/UserManagement/me/username-change-policy',
        method: 'GET',
    });
}

export function useUsernameChangePolicy() {
    const isBrowser = useSyncExternalStore(emptySubscribe, () => true, () => false);
    const hasCredentials = isBrowser && authStorage.hasToken();

    return useQuery({
        queryKey: USERNAME_CHANGE_POLICY_QUERY_KEY,
        queryFn: fetchUsernameChangePolicy,
        enabled: hasCredentials,
        staleTime: 30_000,
    });
}
