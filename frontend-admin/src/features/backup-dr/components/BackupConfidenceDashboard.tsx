"use client";

/**
 * Geriye dönük: testler ve tek kompozit; üretim panosu aynı parçaları BackupDrDashboard içinde sıralar.
 */

import React from "react";
import { Space } from "antd";
import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  RestoreVerificationRunResponseDto,
} from "@/api/generated/model";
import type { DrProofPresentationModel } from "@/features/backup-dr/logic/drProofLevelPresentation";
import { BackupDrDecisionStrip } from "@/features/backup-dr/components/BackupDrDecisionStrip";
import { BackupDrProofSummaryLayers } from "@/features/backup-dr/components/BackupDrProofSummaryLayers";
import { BackupDrRecentEvidenceGrid } from "@/features/backup-dr/components/BackupDrRecentEvidenceGrid";

export interface BackupConfidenceDashboardProps {
  model: DrProofPresentationModel;
  t: (key: string, options?: Record<string, string | number>) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  /** Son yedek çalıştırma isteği — “Son kanıt” ızgarası için. */
  latestRun?: BackupRunResponseDto;
  backupStatusLabel?: (status: number | undefined) => string;
  restoreStatusLabel?: (status: number | undefined) => string;
}

export function BackupConfidenceDashboard({
  model,
  t,
  formatDt,
  formatLocale,
  recoverability,
  restoreLatest,
  latestRun,
  backupStatusLabel = () => "—",
  restoreStatusLabel = () => "—",
}: BackupConfidenceDashboardProps) {
  return (
    <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
      <BackupDrDecisionStrip model={model} t={t} />
      <BackupDrProofSummaryLayers model={model} t={t} />
      <BackupDrRecentEvidenceGrid
        recoverability={recoverability}
        restoreLatest={restoreLatest}
        latestRun={latestRun}
        backupStatusLabel={backupStatusLabel}
        restoreStatusLabel={restoreStatusLabel}
        formatDt={formatDt}
        formatLocale={formatLocale}
        t={t}
      />
    </Space>
  );
}
