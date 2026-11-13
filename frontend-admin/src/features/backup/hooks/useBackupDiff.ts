'use client';

import { useQueries } from '@tanstack/react-query';
import { useMemo } from 'react';

import {
  type BackupDiffViewModel,
  buildBackupDiffViewModel,
} from '@/features/backup/logic/backupDiffPresentation';
import {
  getBackupVerificationReport,
  getBackupVerificationReportQueryKey,
} from '@/features/backup/logic/backupVerificationReportApi';

export type UseBackupDiffOptions = {
  enabled?: boolean;
};

export function useBackupDiff(
  backup1Id: string | null | undefined,
  backup2Id: string | null | undefined,
  options?: UseBackupDiffOptions
) {
  const id1 = backup1Id?.trim() || '';
  const id2 = backup2Id?.trim() || '';
  const bothSet = Boolean(id1 && id2 && id1 !== id2);
  const enabled = options?.enabled !== false && bothSet;

  const [q1, q2] = useQueries({
    queries: [
      {
        queryKey: id1
          ? getBackupVerificationReportQueryKey(id1)
          : ['backup-verification-report', 'idle', 'diff-a'],
        queryFn: () => getBackupVerificationReport(id1),
        enabled: enabled && Boolean(id1),
        staleTime: 60_000,
      },
      {
        queryKey: id2
          ? getBackupVerificationReportQueryKey(id2)
          : ['backup-verification-report', 'idle', 'diff-b'],
        queryFn: () => getBackupVerificationReport(id2),
        enabled: enabled && Boolean(id2),
        staleTime: 60_000,
      },
    ],
  });

  const data: BackupDiffViewModel | null = useMemo(() => {
    if (!bothSet) return null;
    return buildBackupDiffViewModel(q1.data, q2.data);
  }, [bothSet, q1.data, q2.data]);

  const sameId = Boolean(id1 && id2 && id1 === id2);

  return {
    data,
    sameId,
    isLoading: enabled && (q1.isLoading || q2.isLoading) && !data,
    isFetching: q1.isFetching || q2.isFetching,
    isError: enabled && (q1.isError || q2.isError),
    refetch: async () => {
      await Promise.all([q1.refetch(), q2.refetch()]);
    },
  };
}
