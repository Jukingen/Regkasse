'use client';

import { useMemo } from 'react';

import { useBackupVerificationReport } from '@/features/backup/hooks/useBackupVerificationReport';
import {
  type RestorePreviewViewModel,
  mapVerificationReportToRestorePreview,
} from '@/features/backup/logic/restorePreviewPresentation';

export type UseRestorePreviewOptions = {
  enabled?: boolean;
};

/**
 * Restore preview for a backup run — reuses GET .../verification-report
 * (dump TOC + size; row counts are live estimates for dump-scoped tables).
 */
export function useRestorePreview(
  backupRunId: string | null | undefined,
  options?: UseRestorePreviewOptions
) {
  const enabled = options?.enabled !== false && Boolean(backupRunId?.trim());
  const reportQuery = useBackupVerificationReport(backupRunId ?? null, enabled);

  const data: RestorePreviewViewModel | null = useMemo(
    () => mapVerificationReportToRestorePreview(reportQuery.data),
    [reportQuery.data]
  );

  return {
    data,
    report: reportQuery.data,
    isLoading: reportQuery.isLoading && !reportQuery.data,
    isFetching: reportQuery.isFetching,
    isError: reportQuery.isError,
    refetch: reportQuery.refetch,
  };
}
