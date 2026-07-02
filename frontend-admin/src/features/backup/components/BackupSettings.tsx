"use client";

/**
 * Backup schedule + optional manual trigger — permission-gated for Manager (backup.manage)
 * and platform operator (settings.manage / system.critical).
 * Backend has no separate backup.execute; trigger and schedule share backup.manage.
 */

import React from "react";
import { Card, Space } from "antd";
import { usePermissions } from "@/hooks/usePermissions";
import { PERMISSIONS } from "@/shared/auth/permissions";
import { BackupScheduleSettings } from "@/features/backup-dr/components/BackupScheduleSettings";
import { BackupConfigurationForm } from "@/features/backup/components/BackupConfigurationForm";
import { ConfigurationHealthCard } from "@/features/backup/components/ConfigurationHealthCard";
import { TriggerBackupButton } from "@/features/backup/components/TriggerBackupButton";

export interface BackupSettingsProps {
  /** When false, manual trigger is omitted (e.g. page header already exposes it). */
  showManualTrigger?: boolean;
  /** Optional Card title; omit when a parent shell (e.g. Collapse) already labels the section. */
  title?: string | null;
}

export function BackupSettings({ showManualTrigger = false, title = null }: BackupSettingsProps) {
  const { hasPermission } = usePermissions();

  const canManageBackup =
    hasPermission(PERMISSIONS.BACKUP_MANAGE) || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  /** Same gate as schedule edits — no separate backup.execute in backend catalog. */
  const canExecuteBackup = canManageBackup;
  const canConfigureExecution = hasPermission(PERMISSIONS.SETTINGS_MANAGE);

  const content = (
    <Space orientation="vertical" size={16} style={{ width: "100%" }}>
      <ConfigurationHealthCard canManage={canManageBackup} />
      <BackupScheduleSettings canManage={canManageBackup} />
      {canConfigureExecution ? <BackupConfigurationForm /> : null}
      {showManualTrigger && canExecuteBackup ? (
        <TriggerBackupButton canManage={canExecuteBackup} />
      ) : null}
    </Space>
  );

  if (title) {
    return (
      <Card size="small" title={title}>
        {content}
      </Card>
    );
  }

  return content;
}
