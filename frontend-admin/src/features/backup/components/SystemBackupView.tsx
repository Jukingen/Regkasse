"use client";

/**
 * Super Admin backup overview — system-wide dump + structured system package.
 */

import React from "react";
import Link from "next/link";
import { Alert, Card, Space } from "antd";
import { useI18n } from "@/i18n";
import { BackupStats } from "@/features/backup/components/BackupStats";
import { BackupProgress } from "@/features/backup/components/BackupProgress";
import { BackupConfigCard } from "@/features/backup/components/BackupConfigCard";
import { BackupList } from "@/features/backup/components/BackupList";
import { BackupActions } from "@/features/backup/components/BackupActions";
import { BackupDiffPanel } from "@/features/backup/components/BackupDiffPanel";
import { BACKUP_RUNS_PATH } from "@/shared/backupAreaRoutes";

export function SystemBackupView() {
  const { t } = useI18n();

  return (
    <Space orientation="vertical" size={16} style={{ width: "100%" }}>
      <Alert
        type="warning"
        showIcon
        title={t("backupDr.overview.systemView.alertTitle")}
        description={t("backupDr.overview.systemView.alertDescription")}
      />
      <BackupStats />
      <BackupProgress />
      <BackupConfigCard />
      <Card
        size="small"
        title={t("backupDr.overview.systemView.recentTitle")}
        extra={
          <Link href={BACKUP_RUNS_PATH} prefetch={false}>
            {t("backupDr.overview.viewAllRuns")}
          </Link>
        }
      >
        <BackupList limit={5} compact strategyFilter="all" />
      </Card>
      <BackupDiffPanel />
      <BackupActions />
    </Space>
  );
}
