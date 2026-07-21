'use client';

import { useMutation } from '@tanstack/react-query';

import type { DepExportRequestParams, RksvDepExportRoot } from '@/features/rksv/types/depExport';
import { AXIOS_INSTANCE } from '@/lib/axios';

export const useDepExport = () => {
  return useMutation({
    mutationFn: async (params: DepExportRequestParams): Promise<RksvDepExportRoot> => {
      const response = await AXIOS_INSTANCE.get<RksvDepExportRoot>('/api/admin/rksv/dep-export', {
        params,
      });
      return response.data;
    },
  });
};
