'use client';

import { useMutation, useQuery } from '@tanstack/react-query';

import {
  type ValidatePitrRestorePointRequest,
  getPitrAvailability,
  validatePitrRestorePoint,
} from '@/features/backup/logic/backupPitrApi';

export const backupPitrQueryKeys = {
  availability: ['/api/admin/backup/pitr/availability'] as const,
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
