'use client';

import React, { useMemo } from 'react';

import { useGetApiAdminBackupRecoverabilitySummary } from '@/api/generated/admin-backup/admin-backup';
import { BackupStrategyKind } from '@/api/generated/model/backupStrategyKind';
import { BackupComplianceGate } from '@/features/backup/components/BackupComplianceGate';
import { useBackupKnownGaps } from '@/features/backup/hooks/useBackupKnownGaps';
import { useBackupRuns } from '@/features/backup/hooks/useBackupRuns';
import { useRestoreVerificationRuns } from '@/features/backup/hooks/useRestoreVerificationRuns';
import { backupKnownGapLabel } from '@/features/backup/logic/backupKnownGapsApi';
import {
  BACKUP_KNOWN_GAP_LEGAL_HOLD,
  BACKUP_KNOWN_GAP_SCHEDULED_DRILL,
  downgradeGateForAcceptance,
  drillSuccessGate,
  knownGapRowStatus,
  lastScheduledDrillCompletedAt,
  legalHoldGate,
  systemBackupGate,
} from '@/features/backup/logic/backupDrInsights';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/lib/dateUtils';
import { QUERY_STALE_VOLATILE_MS } from '@/lib/query/queryCachePolicy';

function isSucceeded(status: unknown): boolean {
  return status === 'Succeeded' || status === 3;
}

export function BackupComplianceGateCard() {
  const { t } = useI18n();
  const tenant = useTenantProviderValue();
  const known = useBackupKnownGaps(tenant?.tenantId ?? null);
  const summary = useGetApiAdminBackupRecoverabilitySummary({
    query: {
      staleTime: QUERY_STALE_VOLATILE_MS,
      refetchInterval: 15_000,
      refetchOnWindowFocus: true,
    },
  });
  const runs = useBackupRuns({ strategy: BackupStrategyKind.System, page: 1, pageSize: 100 });
  const drills = useRestoreVerificationRuns({ page: 1, pageSize: 100 }, true, {
    staleTime: QUERY_STALE_VOLATILE_MS,
    refetchInterval: 15_000,
  });
  const latest = useMemo(
    () => (runs.data?.items ?? []).find((run) => isSucceeded(run.status)) ?? null,
    [runs.data?.items]
  );
  const drillAt = useMemo(() => {
    if (summary.isSuccess) return summary.data?.lastSuccessfulRestoreProofAt?.trim() || null;
    if (summary.isError) return lastScheduledDrillCompletedAt(drills.data?.items ?? []);
    return null;
  }, [
    summary.isSuccess,
    summary.isError,
    summary.data?.lastSuccessfulRestoreProofAt,
    drills.data?.items,
  ]);

  if (
    (summary.isLoading && !summary.data) ||
    (runs.isLoading && !runs.data) ||
    (drills.isLoading && !drills.data)
  ) {
    return null;
  }

  const systemAt = latest?.completedAt || latest?.requestedAt || null;
  const simulated = Boolean(latest?.isSimulatedExecution);
  const missing = t('backupDr.compliance.gate.missing');
  const gapById = new Map((known.data?.gaps ?? []).map((gap) => [gap.id, gap]));
  const acceptedOpen = (id: string) => {
    const gap = gapById.get(id);
    return Boolean(gap?.open && gap.accepted);
  };
  const acceptedNote = (id: string) => {
    const gap = gapById.get(id);
    if (!gap?.accepted || !gap.acceptedAtUtc) return undefined;
    return t('backupDr.knownGapAcceptance.acceptedOn', { date: formatDateTime(gap.acceptedAtUtc) });
  };
  const drillAccepted = acceptedOpen(BACKUP_KNOWN_GAP_SCHEDULED_DRILL);
  const holdAccepted = acceptedOpen(BACKUP_KNOWN_GAP_LEGAL_HOLD);
  const drillColor = downgradeGateForAcceptance(drillSuccessGate(drillAt), drillAccepted);
  const holdColor = downgradeGateForAcceptance(
    legalHoldGate(
      latest ? { legalHold: latest.legalHold, legalHoldUntilUtc: latest.legalHoldUntilUtc } : null
    ),
    holdAccepted
  );
  const openGaps = (known.data?.gaps ?? []).filter((gap) => gap.open);

  return (
    <BackupComplianceGate
      verdict={known.data ? known.data.verdict : null}
      knownGaps={openGaps.map((gap) => ({
        id: gap.id,
        label: backupKnownGapLabel(t, gap.id, gap.title),
        status: knownGapRowStatus(gap.displayColor),
        note: gap.accepted ? acceptedNote(gap.id) : undefined,
      }))}
      gates={[
        {
          id: 'systemBackup',
          label: t('backupDr.compliance.gate.systemBackup'),
          detail: systemAt
            ? simulated
              ? `${systemAt} · ${t('backupDr.compliance.gate.simulated')}`
              : systemAt
            : missing,
          color: systemBackupGate(systemAt, simulated),
        },
        {
          id: 'legalHold',
          label: t('backupDr.compliance.gate.legalHold'),
          detail: latest?.legalHoldUntilUtc || missing,
          color: holdColor,
          accepted: holdAccepted && holdColor === 'yellow',
          hint: holdAccepted ? acceptedNote(BACKUP_KNOWN_GAP_LEGAL_HOLD) : undefined,
        },
        {
          id: 'drill',
          label: t('backupDr.compliance.gate.drill'),
          detail: drillAt || missing,
          hint: drillAccepted
            ? acceptedNote(BACKUP_KNOWN_GAP_SCHEDULED_DRILL)
            : t('backupDr.compliance.gate.drillThreshold'),
          color: drillColor,
          accepted: drillAccepted && drillColor === 'yellow',
        },
      ]}
    />
  );
}
