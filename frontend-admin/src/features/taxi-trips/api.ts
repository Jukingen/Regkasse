import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminTaxiTripDto = {
  paymentId: string;
  occurredAtUtc: string;
  customerName?: string | null;
  routeFrom?: string | null;
  routeTo?: string | null;
  routeKm?: number | null;
  amount: number;
  cashRegisterNumber?: string | null;
};

export function useAdminTaxiTrips(params: { from?: string; to?: string }) {
  return useQuery({
    queryKey: ['admin', 'taxi-trips', params],
    queryFn: async () => {
      const response = await AXIOS_INSTANCE.get<AdminTaxiTripDto[]>('/api/admin/taxi-trips', {
        params,
      });
      return response.data;
    },
  });
}
