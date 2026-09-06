'use client';

import { useMutation, useQuery } from '@tanstack/react-query';

import {
  type ValidatePitrRestorePointRequest,
  getBackupChain,
  getPitrAvailability,
  getWalArchiveStatus,
  requestPitrDryRun,
  validatePitrPreRestore,
  validatePitrRestorePoint,
} from '@/features/backup/logic/backupPitrApi';

export const backupPitrQueryKeys = {
  availability: ['/api/admin/backup/pitr/availability'] as const,
  wal: ['/api/admin/backup/pitr/wal'] as const,
  chain: (tenantId?: string | null) => ['/api/admin/backup/pitr/chain', tenantId ?? ''] as const,
};

export function usePitrAvailability(enabled: boolean) {
  return useQuery({
    queryKey: backupPitrQueryKeys.availability,
    queryFn: getPitrAvailability,
    enabled,
    staleTime: 30_000,
  });
}

export function useValidatePitrRestorePoint() {
  return useMutation({
    mutationFn: (body: ValidatePitrRestorePointRequest) => validatePitrRestorePoint(body),
  });
}

export function useWalArchiveStatus(enabled: boolean) {
  return useQuery({
    queryKey: backupPitrQueryKeys.wal,
    queryFn: getWalArchiveStatus,
    enabled,
    staleTime: 30_000,
  });
}

export function useBackupChain(enabled: boolean, tenantId?: string | null) {
  return useQuery({
    queryKey: backupPitrQueryKeys.chain(tenantId),
    queryFn: () => getBackupChain(tenantId),
    enabled,
    staleTime: 30_000,
  });
}

export function usePitrPreRestoreValidate() {
  return useMutation({
    mutationFn: (body: ValidatePitrRestorePointRequest) => validatePitrPreRestore(body),
  });
}

export function usePitrDryRun() {
  return useMutation({
    mutationFn: (body: { targetTimeUtc: string; tenantId?: string | null }) =>
      requestPitrDryRun(body),
  });
}
