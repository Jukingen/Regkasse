'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import {
    adminUserToUserInfo,
    adminUsersQueryKeys,
    listPlatformUsers,
} from '@/features/users/api/users';
import type { UserInfo } from '@/features/users/api/usersGateway';

export { adminUsersQueryKeys as platformUsersQueryKey } from '@/features/users/api/users';

export function usePlatformUsersList(
    params?: { isActive?: boolean; query?: string },
    options?: { enabled?: boolean },
) {
    const query = useQuery({
        queryKey: adminUsersQueryKeys.platform(params?.isActive),
        queryFn: () => listPlatformUsers({ isActive: params?.isActive }),
        enabled: options?.enabled ?? true,
        staleTime: 30_000,
    });

    const items = useMemo(() => {
        const mapped = (query.data ?? []).map(adminUserToUserInfo);
        const q = (params?.query ?? '').trim().toLowerCase();
        if (!q) return mapped;
        return mapped.filter((u) => {
            const name = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim().toLowerCase();
            return (
                name.includes(q) ||
                (u.email ?? '').toLowerCase().includes(q) ||
                (u.userName ?? '').toLowerCase().includes(q) ||
                (u.employeeNumber ?? '').toLowerCase().includes(q)
            );
        });
    }, [query.data, params?.query]);

    return { ...query, items: items as UserInfo[] };
}
