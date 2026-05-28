"use client";

import React from "react";
import { Alert, Space } from "antd";
import { BackupConfigurationForm } from "@/features/backup/components/BackupConfigurationForm";
import { ConfigurationHealthCard } from "@/features/backup/components/ConfigurationHealthCard";
import { useBackupManagementAccess } from "@/features/backup-management/hooks/useBackupManagementAccess";
import { useI18n } from "@/i18n";

export function BackupConfigurationTab() {
  const { t } = useI18n();
  const access = useBackupManagementAccess();

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      {access.isReadOnly ? (
        <Alert type="info" showIcon message={t("backupDr.permission.noManage")} />
      ) : null}
      {access.isSuperAdmin ? (
        <Alert type="info" showIcon message={t("backupDr.management.scope.deploymentWide")} />
      ) : null}
      <BackupConfigurationForm />
      <ConfigurationHealthCard canManage={access.canEditConfiguration} poll={false} />
    </Space>
  );
}
