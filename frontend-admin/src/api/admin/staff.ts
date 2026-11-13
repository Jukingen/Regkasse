/**
 * Admin staff directory – GET /api/admin/staff (Cashier, Waiter, Manager).
 */
import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export interface AdminStaffMember {
  id: string;
  name: string;
  role: string;
}

const ADMIN_STAFF = '/api/admin/staff';

export const adminStaffQueryKeys = {
  all: ['admin', 'staff'] as const,
  list: () => [...adminStaffQueryKeys.all, 'list'] as const,
};

export function getAdminStaff(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminStaffMember[]> {
  return customInstance<AdminStaffMember[]>({ url: ADMIN_STAFF, method: 'GET', signal }, options).then(
    (res) => unwrapData<AdminStaffMember[]>(res)
  );
}

export function useAdminStaff(
  enabled = true,
  options?: Partial<UseQueryOptions<AdminStaffMember[], Error, AdminStaffMember[]>>
): UseQueryResult<AdminStaffMember[], Error> {
  return useQuery({
    queryKey: adminStaffQueryKeys.list(),
    queryFn: ({ signal }) => getAdminStaff(undefined, signal),
    enabled,
    ...options,
  });
}
