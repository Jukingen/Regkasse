'use client';

import { useQuery } from '@tanstack/react-query';

import {
  backupKnownGapsQueryKey,
  getBackupKnownGaps,
} from '@/features/backup/logic/backupKnownGapsApi';

export function useBackupKnownGaps(tenantId: string | null | undefined) {
  const id = tenantId?.trim() ?? '';
  return useQuery({
    queryKey: backupKnownGapsQueryKey(id),
    queryFn: () => getBackupKnownGaps(id),
    enabled: id.length > 0,
  });
}
