"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Tabs, Tag } from "antd";
import { useSearchParams } from "next/navigation";
import { useI18n } from "@/i18n";
import { BackupDrDashboard } from "@/features/backup-dr/components/BackupDrDashboard";
import { BackupDashboard } from "@/features/backup/pages/BackupDashboard";
import { BackupConfigurationTab } from "@/features/backup-management/components/BackupConfigurationTab";
import { BackupActivityLogPanel } from "@/features/backup-management/components/BackupActivityLogPanel";
import { BackupDetailModal } from "@/features/backup/components/BackupDetailModal";
import { useBackupManagementAccess } from "@/features/backup-management/hooks/useBackupManagementAccess";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";
import type { BackupRunResponseDto } from "@/api/generated/model";

export type BackupManagementTabKey =
  | "operations"
  | "monitoring"
  | "configuration"
  | "log";

export interface BackupManagementPanelProps {
  defaultTab?: BackupManagementTabKey;
}

export function BackupManagementPanel({ defaultTab = "operations" }: BackupManagementPanelProps) {
  const { t } = useI18n();
  const access = useBackupManagementAccess();
  const searchParams = useSearchParams();
  const [activeTab, setActiveTab] = useState<BackupManagementTabKey>(defaultTab);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);

  useEffect(() => {
    const runId = searchParams.get("runId")?.trim();
    if (!runId) return;
    setSelectedRunId(runId);
    setDetailModalOpen(true);
    setActiveTab("operations");
  }, [searchParams]);

  const openRunDetail = useCallback((run: BackupRunResponseDto) => {
    if (!run.id) return;
    setSelectedRunId(run.id);
    setDetailModalOpen(true);
  }, []);

  const closeRunDetail = useCallback(() => {
    setDetailModalOpen(false);
  }, []);

  const roleTag = access.isSuperAdmin
    ? t("backupDr.management.role.superAdmin")
    : access.canManage
      ? t("backupDr.management.role.tenantAdmin")
      : t("backupDr.management.role.readOnly");

  const tabItems = useMemo(
    () => [
      {
        key: "operations" as const,
        label: t("backupDr.management.tabs.operations"),
        children: (
          <BackupDrDashboard
            embedded
            hideScheduleSettings
            onSelectBackupRun={openRunDetail}
          />
        ),
      },
      {
        key: "monitoring" as const,
        label: t("backupDr.management.tabs.monitoring"),
        children: <BackupDashboard />,
      },
      {
        key: "configuration" as const,
        label: t("backupDr.management.tabs.configuration"),
        children: <BackupConfigurationTab />,
      },
      {
        key: "log" as const,
        label: t("backupDr.management.tabs.log"),
        children: <BackupActivityLogPanel />,
      },
    ],
    [openRunDetail, t],
  );

  return (
    <>
      <Alert
        type={access.isReadOnly ? "info" : "success"}
        showIcon
        style={{ marginBottom: 8 }}
        title={
          <>
            {t("backupDr.management.accessBanner", { role: roleTag })}{" "}
            <Tag>{roleTag}</Tag>
          </>
        }
        description={
          access.isReadOnly
            ? t("backupDr.management.accessReadOnlyHint")
            : access.isSuperAdmin
              ? t("backupDr.management.accessSuperAdminHint")
              : t("backupDr.management.accessManageHint")
        }
      />

      <Tabs
        activeKey={activeTab}
        onChange={(key) => setActiveTab(key as BackupManagementTabKey)}
        items={tabItems}
        destroyInactiveTabPane={false}
      />

      <BackupDetailModal
        runId={selectedRunId}
        open={detailModalOpen}
        onClose={closeRunDetail}
      />
    </>
  );
}
