'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fetchTenantDigitalService,
  fetchTenantDigitalServices,
  toggleTenantDigitalService,
  updateTenantDigitalServicePrice,
  type DigitalServiceType,
  type TenantDigitalServiceRow,
} from '@/features/digital-services/api/tenantDigitalServicesApi';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export const TENANT_DIGITAL_SERVICES_QUERY_KEY = ['admin', 'digital', 'tenants'] as const;

const MANAGE_PERMS = [
  PERMISSIONS.DIGITAL_MANAGE,
  PERMISSIONS.DIGITAL_ACTIVATE,
  PERMISSIONS.DIGITAL_PRICING_MANAGE,
  PERMISSIONS.SYSTEM_CRITICAL,
];

export function useTenantDigitalServices() {
  const { isAuthorized } = useAuthorizationGate({ requiredPermission: MANAGE_PERMS });
  return useQuery({
    queryKey: TENANT_DIGITAL_SERVICES_QUERY_KEY,
    queryFn: fetchTenantDigitalServices,
    enabled: isAuthorized,
  });
}

export function useTenantDigitalService(tenantId: string | undefined) {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: [
      PERMISSIONS.DIGITAL_VIEW,
      PERMISSIONS.DIGITAL_REQUEST,
      PERMISSIONS.DIGITAL_MANAGE,
      PERMISSIONS.WEBSITE_MANAGE,
      PERMISSIONS.SYSTEM_CRITICAL,
    ],
  });
  return useQuery({
    queryKey: [...TENANT_DIGITAL_SERVICES_QUERY_KEY, tenantId],
    queryFn: () => fetchTenantDigitalService(tenantId!),
    enabled: isAuthorized && Boolean(tenantId),
  });
}

export function useToggleTenantDigitalService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: {
      tenantId: string;
      serviceType: DigitalServiceType;
      active: boolean;
      reason?: string;
    }) => toggleTenantDigitalService(args.tenantId, args.serviceType, args.active, args.reason),
    onSuccess: (row) => {
      queryClient.setQueryData<TenantDigitalServiceRow[]>(TENANT_DIGITAL_SERVICES_QUERY_KEY, (prev) => {
        if (!prev) return [row];
        return prev.map((r) => (r.tenantId === row.tenantId ? row : r));
      });
    },
  });
}

export function useUpdateTenantDigitalServicePrice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: {
      tenantId: string;
      serviceType: DigitalServiceType;
      customPrice: number | null;
    }) => updateTenantDigitalServicePrice(args.tenantId, args.serviceType, args.customPrice),
    onSuccess: (row) => {
      queryClient.setQueryData<TenantDigitalServiceRow[]>(TENANT_DIGITAL_SERVICES_QUERY_KEY, (prev) => {
        if (!prev) return [row];
        return prev.map((r) => (r.tenantId === row.tenantId ? row : r));
      });
    },
  });
}
