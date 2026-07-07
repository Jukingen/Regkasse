"use client";

/**
 * Yedekleme yapılandırma sağlığı: adaptör, worker, harici arşiv, doğrulama (API + pipeline policy).
 */

import React from "react";
import Link from "next/link";
import { Alert, Badge, Button, Card, Descriptions, Space } from "antd";
import { CheckCircleOutlined } from "@ant-design/icons";
import type {
  BackupArtifactPipelinePolicyResponseDto,
  BackupConfigurationHealthResponseDto,
} from "@/api/generated/model";
import { mapConfigurationHealthLevel } from "@/features/backup-dr/logic/backupDrMappers";

import { BACKUP_SCHEDULE_SETTINGS_HREF } from '@/shared/backupAreaRoutes';

export interface BackupConfigurationHealthCardProps {
  config: BackupConfigurationHealthResponseDto | undefined;
  artifactPipelinePolicy?: BackupArtifactPipelinePolicyResponseDto;
  canManage?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function isPgDumpAdapterKind(kind: string | null | undefined): boolean {
  return (kind ?? "").trim() === "PgDump";
}

function adapterBadgeStatus(
  kind: string | null | undefined,
): "success" | "warning" | "default" {
  if (!kind?.trim()) return "default";
  return isPgDumpAdapterKind(kind) ? "success" : "warning";
}

export function BackupConfigurationHealthCard({
  config,
  artifactPipelinePolicy,
  canManage = false,
  t,
}: BackupConfigurationHealthCardProps) {
  const uiKind = mapConfigurationHealthLevel(config?.level);
  const isUnhealthy = uiKind === "unhealthy";

  const effectiveAdapter =
    config?.effectiveAdapterKind?.trim() ||
    artifactPipelinePolicy?.effectiveAdapterKind?.trim() ||
    "—";

  const workerEnabled = config?.workerEnabled === true;
  const externalArchiveConfigured =
    artifactPipelinePolicy?.externalArchiveRootConfigured === true;
  const verificationEnabled =
    artifactPipelinePolicy?.stagingOnDiskHashReverificationExpected === true;

  const issues = config?.issues?.filter((x) => x.trim().length > 0) ?? [];

  return (
    <Card
      size="small"
      className={isUnhealthy ? "backup-config-health--unhealthy" : undefined}
      title={t("backupDr.monitoring.configHealth.title")}
      style={isUnhealthy ? { borderColor: "#ff4d4f" } : undefined}
    >
      <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
        <Descriptions.Item label={t("backupDr.monitoring.configHealth.adapterKind")}>
          <Space size="small">
            <Badge status={adapterBadgeStatus(effectiveAdapter)} />
            <span>{effectiveAdapter}</span>
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.monitoring.configHealth.workerEnabled")}>
          <Space size="small">
            <CheckCircleOutlined
              style={{ color: workerEnabled ? "#52c41a" : "#ff4d4f" }}
              aria-hidden
            />
            <span>
              {workerEnabled
                ? t("backupDr.monitoring.configHealth.yes")
                : t("backupDr.monitoring.configHealth.no")}
            </span>
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.monitoring.configHealth.externalArchive")}>
          {externalArchiveConfigured
            ? t("backupDr.monitoring.configHealth.externalArchiveConfigured")
            : t("backupDr.monitoring.configHealth.externalArchiveNotConfigured")}
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.monitoring.configHealth.verification")}>
          {verificationEnabled
            ? t("backupDr.monitoring.configHealth.verificationEnabled")
            : t("backupDr.monitoring.configHealth.verificationDisabled")}
        </Descriptions.Item>
      </Descriptions>

      {issues.length > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 12 }}
          title={t("backupDr.monitoring.configHealth.issuesTitle")}
          description={issues.join(", ")}
        />
      ) : null}

      {canManage ? (
        <Link href={BACKUP_SCHEDULE_SETTINGS_HREF} style={{ marginTop: 8, display: "inline-block" }}>
          <Button type="link" style={{ paddingInline: 0 }}>
            {t("backupDr.monitoring.configHealth.editSettings")}
          </Button>
        </Link>
      ) : null}
    </Card>
  );
}

/** @deprecated Use `BackupConfigurationHealthCard`. */
export const ConfigurationHealthCard = BackupConfigurationHealthCard;
