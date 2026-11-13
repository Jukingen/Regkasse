'use client';

import { useQuery } from '@tanstack/react-query';

import { customInstance } from '@/lib/axios';

export type JahresbelegTenantStatus = {
  tenantId: string;
  tenantName: string;
  year: number;
  deadline: string;
  status: 'created' | 'missing' | 'notDue';
  warning?: string | null;
};

export function useJahresbelegTenantStatus(enabled: boolean) {
  return useQuery({
    queryKey: ['admin', 'jahresbeleg-reminder', 'status'],
    queryFn: ({ signal }) =>
      customInstance<JahresbelegTenantStatus[]>({
        url: '/api/admin/jahresbeleg-reminder/status',
        method: 'GET',
        signal,
      }),
    enabled,
  });
}
