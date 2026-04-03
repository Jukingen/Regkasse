"use client";

/**
 * Birleşik DR kanıt yüzeyi: L0–L6 API kanıtı ile operasyonel kanıt merdiveni tek kartta;
 * iş kuralları değişmez — `buildDrProofPresentationModel` ve `deriveBackupEvidenceLadder` besler.
 */

import React from "react";
import { Card, Divider, Typography } from "antd";
import { BackupEvidenceLadderContent } from "@/features/backup-dr/components/BackupEvidenceLadderCard";
import { BackupDrProofSummaryLayers } from "@/features/backup-dr/components/BackupDrProofSummaryLayers";
import type { BackupDrUnifiedEvidenceModels } from "@/features/backup-dr/logic/backupDrPageEvidenceView";

export interface BackupDrEvidenceSurfaceProps {
  models: BackupDrUnifiedEvidenceModels;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupDrEvidenceSurface({
  models,
  t,
}: BackupDrEvidenceSurfaceProps) {
  const { drProof, evidenceLadder } = models;

  return (
    <Card size="small" styles={{ body: { paddingTop: 12 } }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t("backupDr.confidenceDashboard.subtitle")}
      </Typography.Paragraph>
      <BackupDrProofSummaryLayers model={drProof} t={t} />
      <Divider style={{ margin: "16px 0" }} />
      <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 12 }}>
        {t("backupDr.ia.pipelineEvidenceHeading")}
      </Typography.Title>
      <BackupEvidenceLadderContent model={evidenceLadder} t={t} />
    </Card>
  );
}
