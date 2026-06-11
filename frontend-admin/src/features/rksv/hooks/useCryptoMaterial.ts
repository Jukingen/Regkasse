'use client';

import { useMutation } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';
import type { CryptoMaterial } from '@/features/rksv/types/depExport';

export const useCryptoMaterial = () => {
    return useMutation({
        mutationFn: async (cashRegisterId: string): Promise<CryptoMaterial> => {
            const response = await AXIOS_INSTANCE.get<CryptoMaterial>('/api/admin/rksv/dep-export/test-material', {
                params: { cashRegisterId },
            });
            return response.data;
        },
    });
};
