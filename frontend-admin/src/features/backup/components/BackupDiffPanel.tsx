"use client";

/**
 * BackupDiff with recent succeeded runs as Select options (Super Admin / runs hub).
 */

import React, { useMemo } from "react";
import { useI18n } from "@/i18n";
import { formatDateTime } from "@/i18n/formatting";
import { useBackupRuns } from "@/features/backup/hooks/useBackupRuns";
import { BackupDiff } from "@/features/backup/components/BackupDiff";
import { isBackupRunSucceeded } from "@/features/backup/logic/backupRunDetailPresentation";

export function BackupDiffPanel() {
  const { formatLocale } = useI18n();
  const runsQuery = useBackupRuns({ page: 1, pageSize: 30 }, { enabled: true });

  const runOptions = useMemo(() => {
    const items = runsQuery.data?.items ?? [];
    return items
      .filter((r) => r.id && isBackupRunSucceeded(r.status))
      .map((r) => {
        const when = r.completedAt || r.requestedAt;
        const label = [
          r.id!.slice(0, 8),
          when ? formatDateTime(when, formatLocale) : null,
          r.totalSizeFormatted || null,
        ]
          .filter(Boolean)
          .join(" · ");
        return { value: r.id!, label };
      });
  }, [formatLocale, runsQuery.data?.items]);

  return <BackupDiff runOptions={runOptions} />;
}
