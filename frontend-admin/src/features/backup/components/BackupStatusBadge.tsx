"use client";

import React from "react";
import { Tag } from "antd";
import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  SyncOutlined,
} from "@ant-design/icons";
import { useI18n } from "@/i18n";
import { resolveBackupRunStatusUiKey } from "@/features/backup/logic/backupRunTablePresentation";

export interface BackupStatusBadgeProps {
  status: number | undefined;
}

export function BackupStatusBadge({ status }: BackupStatusBadgeProps) {
  const { t } = useI18n();
  const uiKey = resolveBackupRunStatusUiKey(status);
  const label =
    uiKey === "unknown"
      ? t("backupDr.summary.unknown")
      : t(`backupDr.runsTable.statusLabels.${uiKey}`);

  switch (uiKey) {
    case "succeeded":
      return (
        <Tag color="success" icon={<CheckCircleOutlined />}>
          {label}
        </Tag>
      );
    case "failed":
      return (
        <Tag color="error" icon={<CloseCircleOutlined />}>
          {label}
        </Tag>
      );
    case "verificationFailed":
      return (
        <Tag color="warning" icon={<ExclamationCircleOutlined />}>
          {label}
        </Tag>
      );
    case "running":
    case "awaitingVerification":
      return (
        <Tag color="processing" icon={<SyncOutlined spin />}>
          {label}
        </Tag>
      );
    case "queued":
      return (
        <Tag color="default" icon={<ClockCircleOutlined />}>
          {label}
        </Tag>
      );
    case "cancelled":
      return (
        <Tag color="default" icon={<CloseCircleOutlined />}>
          {label}
        </Tag>
      );
    default:
      return <Tag>{label}</Tag>;
  }
}
