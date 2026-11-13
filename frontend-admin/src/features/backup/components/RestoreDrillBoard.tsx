'use client';

import { Alert } from 'antd';
import React, { useMemo } from 'react';

import { useGetApiAdminBackupRecoverabilitySummary } from '@/api/generated/admin-backup/admin-backup';
import { PageSkeleton } from '@/components/Skeleton';
import { RestoreDrillVisualization } from '@/features/backup/components/RestoreDrillVisualization';
import { useBackupKnownGaps } from '@/features/backup/hooks/useBackupKnownGaps';
import { useRestoreVerificationRuns } from '@/features/backup/hooks/useRestoreVerificationRuns';
import {
  BACKUP_KNOWN_GAP_SCHEDULED_DRILL,
  DEFAULT_DRILL_CADENCE_DAYS,
  drillSchedulePreview,
  drillSuccessRateLast30Days,
  lastScheduledDrillCompletedAt,
  lastSuccessfulDrill,
  toDrillTimeline,
} from '@/features/backup/logic/backupDrInsights';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';
import { QUERY_STALE_VOLATILE_MS } from '@/lib/query/queryCachePolicy';

/** Settled restore-drill poll used by the backup dashboard. */
const DRILL_POLL_MS = 15_000;

export function RestoreDrillBoard() {
  const { t } = useI18n();
  const tenant = useTenantProviderValue();
  const known = useBackupKnownGaps(tenant?.tenantId ?? null);
  const query = useRestoreVerificationRuns({ page: 1, pageSize: 100 }, true, {
    staleTime: QUERY_STALE_VOLATILE_MS,
    refetchInterval: DRILL_POLL_MS,
  });
  const proof = useGetApiAdminBackupRecoverabilitySummary({
    query: {
      staleTime: QUERY_STALE_VOLATILE_MS,
      refetchInterval: DRILL_POLL_MS,
      refetchOnWindowFocus: true,
    },
  });
  const items = useMemo(() => toDrillTimeline(query.data?.items ?? []), [query.data?.items]);
  const rate = useMemo(() => drillSuccessRateLast30Days(items), [items]);
  const proofAt = useMemo(() => {
    if (proof.isSuccess) return proof.data?.lastSuccessfulRestoreProofAt?.trim() || null;
    if (proof.isError) return lastScheduledDrillCompletedAt(query.data?.items ?? []);
    return null;
  }, [proof.isSuccess, proof.isError, proof.data?.lastSuccessfulRestoreProofAt, query.data?.items]);
  const last = useMemo(
    () =>
      lastSuccessfulDrill(
        proofAt
          ? [{ id: 'scheduled-proof', at: proofAt, durationMs: null, result: 'Succeeded' as const, reason: '', reportable: false }]
          : []
      ),
    [proofAt]
  );
  const schedule = useMemo(() => drillSchedulePreview(last.at), [last.at]);
  const drillGap = known.data?.gaps?.find((gap) => gap.id === BACKUP_KNOWN_GAP_SCHEDULED_DRILL);
  const overdueAccepted = schedule.overdue && Boolean(drillGap?.accepted);
  const acceptedNote =
    overdueAccepted && drillGap?.acceptedAtUtc
      ? t('backupDr.knownGapAcceptance.acceptedOn', { date: formatDateTime(drillGap.acceptedAtUtc) })
      : null;

  if (query.isLoading || (proof.isLoading && !proof.data)) return <PageSkeleton widgets={3} />;
  if (query.isError) {
    return <Alert type="error" showIcon title={t('backupDr.drills.empty')} />;
  }

  return (
    <RestoreDrillVisualization
      items={items}
      successRate={rate.rate}
      lastSuccessAt={last.at}
      daysSince={last.daysSince}
      nextDrillAt={schedule.nextAt}
      overdue={schedule.overdue}
      overdueAccepted={overdueAccepted}
      acceptedNote={acceptedNote}
      cadenceDays={DEFAULT_DRILL_CADENCE_DAYS}
    />
  );
}
