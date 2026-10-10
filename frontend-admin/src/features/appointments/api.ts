import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminAppointmentDto = {
  id: string;
  startUtc: string;
  endUtc: string;
  staffId?: string | null;
  status: string;
  notes?: string | null;
};

export async function listAdminAppointments(params: {
  from?: string;
  to?: string;
  staffId?: string;
  status?: string;
}): Promise<AdminAppointmentDto[]> {
  const response = await AXIOS_INSTANCE.get<AdminAppointmentDto[]>('/api/admin/appointments', {
    params,
  });
  return response.data;
}

export function useAdminAppointments(params: {
  from?: string;
  to?: string;
  staffId?: string;
  status?: string;
}) {
  return useQuery({
    queryKey: ['admin', 'appointments', params],
    queryFn: () => listAdminAppointments(params),
  });
}
