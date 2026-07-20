'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  approveDigitalServiceRequest,
  fetchDigitalServiceRequests,
  fetchTenantDigitalServiceRequests,
  rejectDigitalServiceRequest,
  requestDigitalService,
  type DigitalServiceRequest,
} from '@/features/digital-services/api/digitalServiceRequestsApi';
import type { DigitalServiceType } from '@/features/digital-services/api/tenantDigitalServicesApi';
import { TENANT_DIGITAL_SERVICES_QUERY_KEY } from '@/features/digital-services/hooks/useTenantDigitalServices';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export const DIGITAL_SERVICE_REQUESTS_QUERY_KEY = ['admin', 'digital', 'requests'] as const;

export type DigitalServiceRequestListFilter = 'Pending' | 'Approved' | 'Rejected' | 'all';

export function useTenantDigitalServiceRequests(tenantId: string | undefined) {
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
    queryKey: [...DIGITAL_SERVICE_REQUESTS_QUERY_KEY, 'tenant', tenantId],
    queryFn: () => fetchTenantDigitalServiceRequests(tenantId!),
    enabled: isAuthorized && Boolean(tenantId),
  });
}

export function useDigitalServiceRequests(
  status: DigitalServiceRequestListFilter = 'Pending',
) {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  });
  return useQuery({
    queryKey: [...DIGITAL_SERVICE_REQUESTS_QUERY_KEY, status],
    queryFn: () => fetchDigitalServiceRequests(status),
    enabled: isAuthorized,
  });
}

/** @deprecated Prefer useDigitalServiceRequests('Pending') */
export function usePendingDigitalServiceRequests() {
  return useDigitalServiceRequests('Pending');
}

export function useRequestDigitalService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { tenantId: string; serviceType: DigitalServiceType; note?: string }) =>
      requestDigitalService(args.tenantId, args.serviceType, args.note),
    onSuccess: (row) => {
      void queryClient.invalidateQueries({
        queryKey: [...DIGITAL_SERVICE_REQUESTS_QUERY_KEY, 'tenant', row.tenantId],
      });
      void queryClient.invalidateQueries({
        queryKey: DIGITAL_SERVICE_REQUESTS_QUERY_KEY,
      });
      void queryClient.invalidateQueries({
        queryKey: TENANT_DIGITAL_SERVICES_QUERY_KEY,
      });
    },
  });
}

function invalidateAfterResolve(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: DIGITAL_SERVICE_REQUESTS_QUERY_KEY });
  void queryClient.invalidateQueries({ queryKey: TENANT_DIGITAL_SERVICES_QUERY_KEY });
}

export function useApproveDigitalServiceRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { id: string; note?: string }) =>
      approveDigitalServiceRequest(args.id, args.note),
    onSuccess: () => invalidateAfterResolve(queryClient),
  });
}

export function useRejectDigitalServiceRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { id: string; note?: string }) =>
      rejectDigitalServiceRequest(args.id, args.note),
    onSuccess: () => invalidateAfterResolve(queryClient),
  });
}

export function findPendingRequest(
  requests: DigitalServiceRequest[] | undefined,
  serviceType: DigitalServiceType,
): DigitalServiceRequest | undefined {
  return requests?.find(
    (r) => r.serviceType === serviceType && r.status === 'Pending',
  );
}
