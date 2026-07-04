'use client';

import { useMemo, useState } from 'react';

import type { UsersListParams } from '@/features/users/api/usersGateway';
import { useTenantStaff } from '@/features/staff/hooks/useTenantStaff';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;

export type StaffListFilters = {
    role?: string;
    isActive?: boolean;
    query?: string;
};

/**
 * Staff list with server-side pagination + search/role/status filters.
 */
export function useStaffList(options?: { enabled?: boolean }) {
    const [page, setPage] = useState(DEFAULT_PAGE);
    const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
    const [filters, setFilters] = useState<StaffListFilters>({ isActive: true });

    const params = useMemo(
        (): UsersListParams => ({
            page,
            pageSize,
            role: filters.role,
            isActive: filters.isActive,
            query: filters.query?.trim() || undefined,
        }),
        [filters.isActive, filters.query, filters.role, page, pageSize],
    );

    const query = useTenantStaff(params, options);

    const setSearch = (queryText: string) => {
        setFilters((prev) => ({ ...prev, query: queryText }));
        setPage(DEFAULT_PAGE);
    };

    const setRoleFilter = (role: string | undefined) => {
        setFilters((prev) => ({ ...prev, role }));
        setPage(DEFAULT_PAGE);
    };

    const setStatusFilter = (isActive: boolean | undefined) => {
        setFilters((prev) => ({ ...prev, isActive }));
        setPage(DEFAULT_PAGE);
    };

    return {
        ...query,
        page,
        pageSize,
        filters,
        setPage,
        setPageSize,
        setSearch,
        setRoleFilter,
        setStatusFilter,
    };
}
