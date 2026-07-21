'use client';

import React, { useCallback } from 'react';

import {
  useGetApiAdminBackupRecoverabilitySummary,
  useGetApiAdminBackupStatusLatest,
} from '@/api/generated/admin-backup/admin-backup';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';
import {
  usePollAlignedWithLatestDashboardBackup,
  usePollBackupLatestDashboardInterval,
} from '@/features/backup-dr/logic/backupDashboardQueryTiming';
import { apiNullableToUndefined } from '@/features/backup-dr/logic/backupDrDtoNormalize';

export interface BackupRecoverabilityCardProps {
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  backupStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  simulatedOperationalMode?: boolean;
  omitSimulatedEnvironmentStrip?: boolean;
  omitProofGapCaveat?: boolean;
  hideProofTimestampBlock?: boolean;
  t: (k: string) => string;
}

/** Recoverability digest: aligns secondary GET polling with backup activity (legacy dashboard parity). */
export function BackupRecoverabilityCard({
  formatDt,
  formatLocale,
  backupStatusLabel,
  restoreStatusLabel,
  simulatedOperationalMode = false,
  omitSimulatedEnvironmentStrip = false,
  omitProofGapCaveat = false,
  hideProofTimestampBlock = false,
  t,
}: BackupRecoverabilityCardProps) {
  const pollBackupPeek = usePollBackupLatestDashboardInterval();
  const statusPeek = useGetApiAdminBackupStatusLatest({
    query: {
      refetchInterval: pollBackupPeek,
      refetchOnWindowFocus: true,
    },
  });
  const latestPeek = apiNullableToUndefined(statusPeek.data?.latestRun);
  const pollAlignedRecoverability = usePollAlignedWithLatestDashboardBackup(latestPeek?.status);

  const recoverabilityQuery = useGetApiAdminBackupRecoverabilitySummary({
    query: {
      refetchInterval: pollAlignedRecoverability,
      refetchOnWindowFocus: true,
    },
  });

  const onRetry = useCallback(() => void recoverabilityQuery.refetch(), [recoverabilityQuery]);

  return (
    <RecoverabilitySummaryCard
      summary={recoverabilityQuery.data}
      loading={recoverabilityQuery.isLoading}
      queryError={recoverabilityQuery.isError}
      onRetry={onRetry}
      formatDt={formatDt}
      formatLocale={formatLocale}
      backupStatusLabel={backupStatusLabel}
      restoreStatusLabel={restoreStatusLabel}
      simulatedOperationalMode={simulatedOperationalMode}
      omitSimulatedEnvironmentStrip={omitSimulatedEnvironmentStrip}
      omitProofGapCaveat={omitProofGapCaveat}
      hideProofTimestampBlock={hideProofTimestampBlock}
      t={t}
    />
  );
}
