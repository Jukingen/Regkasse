'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type DataRightsRequestType,
  confirmDataRightsDelete,
  confirmTenantDataDeletion,
  createDataRightsRequest,
  downloadDataRightsExport,
  downloadTenantDataExport,
  executeDataRightsDelete,
  executeTenantDataPurge,
  getTenantDataManagementSummary,
  listDataRightsRequestTypes,
  listDataRightsRequests,
  requestTenantDataDeletion,
} from '@/features/data-management/api/tenantDataManagement';

export const tenantDataManagementQueryKey = (tenantId: string) =>
  ['tenant-data-management', tenantId] as const;

export const dataRightsRequestsQueryKey = (tenantId: string) =>
  ['tenant-data-rights-requests', tenantId] as const;

export const dataRightsCatalogQueryKey = (tenantId: string) =>
  ['tenant-data-rights-catalog', tenantId] as const;

function invalidateDataManagement(
  queryClient: ReturnType<typeof useQueryClient>,
  tenantId: string
) {
  void queryClient.invalidateQueries({ queryKey: tenantDataManagementQueryKey(tenantId) });
  void queryClient.invalidateQueries({ queryKey: dataRightsRequestsQueryKey(tenantId) });
}

export function useTenantDataManagementSummary(tenantId: string) {
  return useQuery({
    queryKey: tenantDataManagementQueryKey(tenantId),
    queryFn: () => getTenantDataManagementSummary(tenantId),
    enabled: Boolean(tenantId),
  });
}

export function useDataRightsRequestTypes(tenantId: string) {
  return useQuery({
    queryKey: dataRightsCatalogQueryKey(tenantId),
    queryFn: () => listDataRightsRequestTypes(tenantId),
    enabled: Boolean(tenantId),
  });
}

export function useDataRightsRequests(tenantId: string) {
  return useQuery({
    queryKey: dataRightsRequestsQueryKey(tenantId),
    queryFn: () => listDataRightsRequests(tenantId),
    enabled: Boolean(tenantId),
    refetchInterval: (query) => {
      const rows = query.state.data;
      if (
        !rows?.some(
          (r) =>
            r.requestType === 'export' && (r.status === 'processing' || r.status === 'approved')
        )
      ) {
        return false;
      }
      return 10_000;
    },
  });
}

export function useCreateDataRightsRequest(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { type: DataRightsRequestType; reason?: string }) =>
      createDataRightsRequest(tenantId, input.type, input.reason),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useDownloadDataRightsExport(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => downloadDataRightsExport(tenantId, requestId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useConfirmDataRightsDelete(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => confirmDataRightsDelete(tenantId, requestId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useExecuteDataRightsDelete(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => executeDataRightsDelete(tenantId, requestId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useExportTenantData(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => downloadTenantDataExport(tenantId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useRequestTenantDataDeletion(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reason?: string) => requestTenantDataDeletion(tenantId, reason),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useConfirmTenantDataDeletion(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => confirmTenantDataDeletion(tenantId, requestId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}

export function useExecuteTenantDataPurge(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => executeTenantDataPurge(tenantId, requestId),
    onSuccess: () => invalidateDataManagement(queryClient, tenantId),
  });
}
