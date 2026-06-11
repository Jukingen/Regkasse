'use client';

import { useMutation } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';
import type { DepExportRequestParams, RksvDepExportRoot } from '@/features/rksv/types/depExport';

export const useDepExport = () => {
    return useMutation({
        mutationFn: async (params: DepExportRequestParams): Promise<RksvDepExportRoot> => {
            const response = await AXIOS_INSTANCE.get<RksvDepExportRoot>('/api/admin/rksv/dep-export', { params });
            return response.data;
        },
    });
};
