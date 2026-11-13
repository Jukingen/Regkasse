import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminImeiDto = {
  id: string;
  productId: string;
  productName: string;
  imei: string;
  status: string;
  soldAtUtc?: string | null;
  warrantyMonths: number;
};

export function useAdminImeis(params: { productId?: string; status?: string }) {
  return useQuery({
    queryKey: ['admin', 'product-imeis', params],
    queryFn: async () => {
      const response = await AXIOS_INSTANCE.get<AdminImeiDto[]>('/api/admin/product-imeis', {
        params,
      });
      return response.data;
    },
  });
}
