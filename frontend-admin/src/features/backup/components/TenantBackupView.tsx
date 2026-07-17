"use client";

/**
 * Mandanten-Admin backup overview — tenant-scoped packages only (no Identity / platform).
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
import { BACKUP_RUNS_PATH } from "@/shared/backupAreaRoutes";

export function TenantBackupView() {
  const { t } = useI18n();

  return (
    <Space orientation="vertical" size={16} style={{ width: "100%" }}>
      <Alert
        type="info"
        showIcon
        title={t("backupDr.overview.tenantView.alertTitle")}
        description={t("backupDr.overview.tenantView.alertDescription")}
      />
      <BackupStats />
      <BackupProgress />
      <BackupConfigCard />
      <Card
        size="small"
        title={t("backupDr.overview.tenantView.recentTitle")}
        extra={
          <Link href={BACKUP_RUNS_PATH} prefetch={false}>
            {t("backupDr.overview.viewAllRuns")}
          </Link>
        }
      >
        <BackupList limit={5} compact strategyFilter="tenant" />
      </Card>
      <BackupActions />
    </Space>
  );
}
