'use client';

import { useQuery } from '@tanstack/react-query';
import { fetchCustomerServices } from '@/features/website-generator/api/customerPortalApi';

export function useCustomerServices(tenantId?: string) {
  return useQuery({
    queryKey: ['admin', 'website', 'my-services', tenantId ?? 'ambient'],
    queryFn: () => fetchCustomerServices(tenantId),
  });
}
