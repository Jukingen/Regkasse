'use client';

import { useQuery } from '@tanstack/react-query';
import {
  getBackupVerificationReport,
  getBackupVerificationReportQueryKey,
} from '@/features/backup/logic/backupVerificationReportApi';

export function useBackupVerificationReport(backupRunId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: backupRunId ? getBackupVerificationReportQueryKey(backupRunId) : ['backup-verification-report', 'idle'],
    queryFn: () => getBackupVerificationReport(backupRunId!),
    enabled: enabled && Boolean(backupRunId),
    staleTime: 60_000,
  });
}
