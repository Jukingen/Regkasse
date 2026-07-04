"use client";

import React, { useCallback, useMemo, useState } from "react";
import { Alert, Card, Space, Table, Tag, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { useQuery } from "@tanstack/react-query";
import { useGetApiAuditLog } from "@/api/generated/audit-log/audit-log";
import type { AuditLogEntryDto } from "@/api/generated/model";
import { fetchActivities, type ActivityDto } from "@/api/manual/activityEvents";
import { useBackupManagementAccess } from "@/features/backup-management/hooks/useBackupManagementAccess";
import { useI18n } from "@/i18n";
import { formatDateTime } from "@/i18n/formatting";

const BACKUP_ACTIVITY_TYPE_PREFIXES = ["backup_", "restore_"] as const;

const BACKUP_AUDIT_ENTITY_TYPES = [
  "BackupArtifact",
  "BackupRun",
  "BackupScheduleConfiguration",
  "BackupRuntimeExecutionPreference",
] as const;

const BACKUP_AUDIT_ACTION_PREFIXES = ["BACKUP_"] as const;

function isBackupActivity(row: ActivityDto): boolean {
  const type = (row.type ?? "").toLowerCase();
  const entity = (row.entityType ?? "").toLowerCase();
  return (
    BACKUP_ACTIVITY_TYPE_PREFIXES.some((p) => type.startsWith(p)) ||
    entity.includes("backup") ||
    entity.includes("restore_verification")
  );
}

function isBackupAuditRow(row: AuditLogEntryDto): boolean {
  const action = (row.action ?? "").toUpperCase();
  const entity = row.entityType ?? "";
  if (BACKUP_AUDIT_ACTION_PREFIXES.some((p) => action.startsWith(p))) return true;
  return (BACKUP_AUDIT_ENTITY_TYPES as readonly string[]).includes(entity);
}

export function BackupActivityLogPanel() {
  const { t, formatLocale } = useI18n();
  const access = useBackupManagementAccess();
  const [activityPage, setActivityPage] = useState(1);
  const activityPageSize = 20;

  const activitiesQuery = useQuery({
    queryKey: ["backup-management", "activities"],
    queryFn: ({ signal }) =>
      fetchActivities(
        {
          limit: 100,
          offset: 0,
        },
        signal,
      ),
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });

  const auditQuery = useGetApiAuditLog(
    {
      page: 1,
      pageSize: 100,
    },
    {
      query: {
        enabled: access.canViewAudit,
        staleTime: 60_000,
        refetchOnWindowFocus: false,
      },
    },
  );

  const backupActivities = useMemo(() => {
    const items = activitiesQuery.data?.items ?? [];
    return items.filter(isBackupActivity);
  }, [activitiesQuery.data?.items]);

  const pagedActivities = useMemo(() => {
    const start = (activityPage - 1) * activityPageSize;
    return backupActivities.slice(start, start + activityPageSize);
  }, [activityPage, activityPageSize, backupActivities]);

  const auditRows = useMemo(() => {
    const rows = auditQuery.data?.auditLogs ?? [];
    return rows.filter(isBackupAuditRow);
  }, [auditQuery.data?.auditLogs]);

  const formatDt = useCallback(
    (iso: string | undefined | null) => {
      if (!iso) return "—";
      return formatDateTime(iso, formatLocale);
    },
    [formatLocale],
  );

  const activityColumns: ColumnsType<ActivityDto> = useMemo(
    () => [
      {
        title: t("backupDr.management.log.col.time"),
        dataIndex: "createdAtUtc",
        key: "time",
        render: (v: string) => formatDt(v),
      },
      {
        title: t("backupDr.management.log.col.severity"),
        dataIndex: "severity",
        key: "severity",
        render: (s: string) => <Tag>{s}</Tag>,
      },
      {
        title: t("backupDr.management.log.col.type"),
        dataIndex: "type",
        key: "type",
      },
      {
        title: t("backupDr.management.log.col.title"),
        dataIndex: "title",
        key: "title",
      },
      {
        title: t("backupDr.management.log.col.actor"),
        key: "actor",
        render: (_: unknown, row: ActivityDto) => row.actorName?.trim() || row.actorUserId || "—",
      },
    ],
    [formatDt, t],
  );

  const auditColumns: ColumnsType<AuditLogEntryDto> = useMemo(
    () => [
      {
        title: t("backupDr.management.log.col.time"),
        dataIndex: "timestamp",
        key: "time",
        render: (v: string | undefined) => formatDt(v),
      },
      {
        title: t("backupDr.management.log.col.action"),
        dataIndex: "action",
        key: "action",
        render: (a: string | undefined) => <Tag>{a ?? "—"}</Tag>,
      },
      {
        title: t("backupDr.management.log.col.entity"),
        key: "entity",
        render: (_: unknown, row: AuditLogEntryDto) =>
          `${row.entityType ?? "—"}${row.entityId ? ` · ${row.entityId}` : ""}`,
      },
      {
        title: t("backupDr.management.log.col.description"),
        dataIndex: "description",
        key: "description",
        ellipsis: true,
      },
    ],
    [formatDt, t],
  );

  return (
    <Space orientation="vertical" size={16} style={{ width: "100%" }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t("backupDr.management.log.subtitle")}
      </Typography.Paragraph>

      <Card size="small" title={t("backupDr.management.log.activityTitle")}>
        {activitiesQuery.isError ? (
          <Alert type="error" showIcon title={t("backupDr.errors.loadFailed")} />
        ) : (
          <Table<ActivityDto>
            rowKey="id"
            size="small"
            loading={activitiesQuery.isFetching}
            dataSource={pagedActivities}
            columns={activityColumns}
            pagination={{
              current: activityPage,
              pageSize: activityPageSize,
              total: backupActivities.length,
              showSizeChanger: false,
              onChange: (p) => setActivityPage(p),
            }}
            locale={{ emptyText: t("backupDr.management.log.emptyActivity") }}
          />
        )}
      </Card>

      {access.canViewAudit ? (
        <Card size="small" title={t("backupDr.management.log.auditTitle")}>
          {auditQuery.isError ? (
            <Alert type="error" showIcon title={t("backupDr.errors.loadFailed")} />
          ) : (
            <Table<AuditLogEntryDto>
              rowKey="id"
              size="small"
              loading={auditQuery.isFetching}
              dataSource={auditRows}
              columns={auditColumns}
              pagination={false}
              locale={{ emptyText: t("backupDr.management.log.emptyAudit") }}
            />
          )}
        </Card>
      ) : (
        <Alert
          type="info"
          showIcon
          title={t("backupDr.management.log.auditRestricted")}
        />
      )}
    </Space>
  );
}
