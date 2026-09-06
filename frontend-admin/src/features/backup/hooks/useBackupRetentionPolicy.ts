'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { getGetApiAdminBackupRunsQueryKey } from '@/api/generated/admin-backup/admin-backup';
import { invalidateBackupQueries } from '@/features/backup/api/backupHooks';
import {
  getBackupRetentionPolicy,
  getBackupRetentionPolicyQueryKey,
  moveBackupRunToCold,
  putBackupRetentionPolicy,
  type BackupRetentionPolicyPutRequest,
} from '@/features/backup/logic/backupRetentionPolicyApi';
import { getBackupStorageCostsQueryKey } from '@/features/backup/logic/backupStorageCostsApi';

export function useBackupRetentionPolicy(options?: { enabled?: boolean }) {
  const enabled = options?.enabled !== false;
  const query = useQuery({
    queryKey: getBackupRetentionPolicyQueryKey(),
    queryFn: getBackupRetentionPolicy,
    enabled,
    staleTime: 30_000,
  });

  return {
    data: query.data ?? null,
    isLoading: query.isLoading && !query.data,
    isError: query.isError,
    refetch: query.refetch,
  };
}

export function useSaveBackupRetentionPolicy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: BackupRetentionPolicyPutRequest) => putBackupRetentionPolicy(body),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: getBackupRetentionPolicyQueryKey() });
      await queryClient.invalidateQueries({ queryKey: getBackupStorageCostsQueryKey() });
    },
  });
}

export function useMoveBackupRunToCold() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => moveBackupRunToCold(runId),
    onSuccess: async () => {
      await invalidateBackupQueries(queryClient);
      await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRunsQueryKey() });
      await queryClient.invalidateQueries({ queryKey: getBackupRetentionPolicyQueryKey() });
      await queryClient.invalidateQueries({ queryKey: getBackupStorageCostsQueryKey() });
    },
  });
}
