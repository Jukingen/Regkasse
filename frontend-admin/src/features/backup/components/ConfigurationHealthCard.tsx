"use client";

/**
 * Yedekleme yapılandırma sağlığı kartı — tanılar, adaptör, worker ve harici arşiv özeti.
 */

import React, { useMemo } from "react";
import Link from "next/link";
import {
  Badge,
  Button,
  Card,
  Spin,
  Tag,
  Typography,
} from "antd";
import { SimpleList as List } from "@/components/ui/SimpleList";
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import type { BackupConfigurationDiagnosticResponseDto } from "@/api/generated/model";
import { useI18n } from "@/i18n";
import { formatDateTime } from "@/i18n/formatting";
import { useBackupConfigurationHealth } from "@/features/backup/hooks/useBackupConfigurationHealth";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";

const BACKUP_SETTINGS_HASH = "#backup-dr-schedule-settings";

type HealthLevelKey = "Healthy" | "Degraded" | "Unhealthy";

function resolveHealthLevelKey(level: string | null | undefined): HealthLevelKey | null {
  const n = (level ?? "").trim().toLowerCase();
  if (n === "healthy") return "Healthy";
  if (n === "degraded") return "Degraded";
  if (n === "unhealthy") return "Unhealthy";
  return null;
}

function diagnosticItems(
  diagnostics: BackupConfigurationDiagnosticResponseDto[] | null | undefined,
  issues: string[] | null | undefined,
): BackupConfigurationDiagnosticResponseDto[] {
  const fromDiagnostics = diagnostics?.filter((d) => (d.message ?? d.code)?.trim()) ?? [];
  if (fromDiagnostics.length > 0) return fromDiagnostics;
  return (issues ?? [])
    .filter((x) => x.trim().length > 0)
    .map((message) => ({ message, code: "issue", severity: "Warning" }));
}

export type ConfigurationHealthCardProps = {
  canManage?: boolean;
  /** Dashboard polling; default true. */
  poll?: boolean;
};

export function ConfigurationHealthCard({
  canManage: canManageProp,
  poll = true,
}: ConfigurationHealthCardProps) {
  const { t, formatLocale } = useI18n();
  const { canManageBackup } = useBackupPermissions();
  const canManage = canManageProp ?? canManageBackup;
  const { health, isLoading, isError, updatedAt } = useBackupConfigurationHealth({ poll });

  const levelKey = resolveHealthLevelKey(health?.level);

  const healthConfig = useMemo(
    () =>
      ({
        Healthy: {
          color: "#52c41a",
          icon: <CheckCircleOutlined />,
          text: t("backupDr.monitoring.configHealth.levelHealthy"),
        },
        Degraded: {
          color: "#faad14",
          icon: <ExclamationCircleOutlined />,
          text: t("backupDr.monitoring.configHealth.levelDegraded"),
        },
        Unhealthy: {
          color: "#ff4d4f",
          icon: <CloseCircleOutlined />,
          text: t("backupDr.monitoring.configHealth.levelUnhealthy"),
        },
      }) satisfies Record<HealthLevelKey, { color: string; icon: React.ReactNode; text: string }>,
    [t],
  );

  const badge = levelKey ? healthConfig[levelKey] : undefined;
  const listData = diagnosticItems(health?.diagnostics, health?.issues);

  if (isLoading && !health) {
    return (
      <Card title={t("backupDr.monitoring.configHealth.title")} size="small">
        <Spin />
      </Card>
    );
  }

  return (
    <Card
      size="small"
      className={levelKey === "Unhealthy" ? "backup-config-health--unhealthy" : undefined}
      title={t("backupDr.monitoring.configHealth.title")}
      style={levelKey === "Unhealthy" ? { borderColor: "#ff4d4f" } : undefined}
      extra={
        badge ? (
          <Badge color={badge.color} text={<span>{badge.text}</span>} />
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        )
      }
    >
      {isError ? (
        <Typography.Text type="danger">{t("backupDr.errors.loadFailed")}</Typography.Text>
      ) : null}

      <List
        size="small"
        locale={{ emptyText: t("backupDr.monitoring.configHealth.noDiagnostics") }}
        dataSource={listData}
        renderItem={(item) => {
          const severity = (item.severity ?? "").trim().toLowerCase();
          const tagColor = severity === "error" ? "red" : "orange";
          return (
            <List.Item>
              <div className="flex w-full items-center justify-between gap-3">
                <span>{item.message ?? item.code ?? "—"}</span>
                {item.code ? (
                  <Tag color={tagColor}>{item.code}</Tag>
                ) : null}
              </div>
            </List.Item>
          );
        }}
      />

      {health?.effectiveAdapterKind != null || health?.workerEnabled != null ? (
        <div className="mt-4 rounded bg-gray-50 p-3">
          <div>
            <strong>{t("backupDr.monitoring.configHealth.adapterKind")}:</strong>{" "}
            {health?.effectiveAdapterKind?.trim() || "—"}
          </div>
          <div>
            <strong>{t("backupDr.monitoring.configHealth.workerEnabled")}:</strong>{" "}
            {health?.workerEnabled
              ? t("backupDr.monitoring.configHealth.yes")
              : t("backupDr.monitoring.configHealth.no")}
          </div>
          <div>
            <strong>{t("backupDr.monitoring.configHealth.externalArchive")}:</strong>{" "}
            {health?.externalArchiveRootConfigured
              ? t("backupDr.monitoring.configHealth.externalArchiveConfigured")
              : t("backupDr.monitoring.configHealth.externalArchiveNotConfigured")}
          </div>
        </div>
      ) : null}

      {canManage ? (
        <Link href={`/settings/backup-dr${BACKUP_SETTINGS_HASH}`} className="mt-3 inline-block">
          <Button type="link" style={{ paddingInline: 0 }}>
            {t("backupDr.monitoring.configHealth.editSettings")}
          </Button>
        </Link>
      ) : null}

      <div className="mt-4 text-xs text-gray-400">
        {t("backupDr.monitoring.configHealth.lastUpdated", {
          at: updatedAt ? formatDateTime(updatedAt, formatLocale) : "—",
        })}
      </div>
    </Card>
  );
}
