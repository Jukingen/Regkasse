'use client';

import { useMutation } from '@tanstack/react-query';

import type { CryptoMaterial } from '@/features/rksv/types/depExport';
import { AXIOS_INSTANCE } from '@/lib/axios';

export const useCryptoMaterial = () => {
  return useMutation({
    mutationFn: async (cashRegisterId: string): Promise<CryptoMaterial> => {
      const response = await AXIOS_INSTANCE.get<CryptoMaterial>(
        '/api/admin/rksv/dep-export/test-material',
        {
          params: { cashRegisterId },
        }
      );
      return response.data;
    },
  });
};
