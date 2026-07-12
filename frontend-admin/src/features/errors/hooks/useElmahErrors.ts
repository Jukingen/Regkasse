'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';

export type ElmahErrorRow = {
    errorId: string;
    application: string;
    host: string;
    type: string;
    source: string;
    message: string;
    user?: string | null;
    statusCode: number;
    timeUtc: string;
    allXml?: string | null;
};

type ElmahErrorListResponse = {
    items?: ElmahErrorRow[] | null;
    totalCount?: number;
};

async function fetchElmahErrors(): Promise<ElmahErrorRow[]> {
    const { data } = await customInstance<ElmahErrorListResponse>({
        url: '/api/admin/errors',
        method: 'GET',
        params: { page: 1, pageSize: 100 },
    });
    return data.items ?? [];
}

export function useElmahErrors() {
    return useQuery({
        queryKey: ['elmah-errors'],
        queryFn: fetchElmahErrors,
        placeholderData: keepPreviousData,
    });
}
