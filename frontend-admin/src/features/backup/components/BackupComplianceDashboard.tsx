"use client";

/**
 * RKSV product-gate compliance dashboard (restore-readiness metadata, not BMF certification).
 */

import React, { useMemo } from "react";
import { Alert, Card, Col, Row, Spin, Statistic, Table, Tag, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { useI18n } from "@/i18n";
import { useComplianceStatus } from "@/features/backup/hooks/useComplianceStatus";
import type { BackupComplianceListItemDto } from "@/features/backup/logic/backupComplianceStatusApi";

function reasonLabelKey(reason: string): string {
  switch (reason) {
    case "system_dump_hash_present":
      return "backupDr.compliance.reasons.systemDumpHashPresent";
    case "tenant_package_integrity_ok":
      return "backupDr.compliance.reasons.tenantPackageIntegrityOk";
    case "missing_sha256":
      return "backupDr.compliance.reasons.missingSha256";
    case "missing_logical_dump":
      return "backupDr.compliance.reasons.missingLogicalDump";
    case "backup_not_succeeded":
      return "backupDr.compliance.reasons.backupNotSucceeded";
    default:
      return "backupDr.compliance.reasons.unknown";
  }
}

export function BackupComplianceDashboard() {
  const { t, formatLocale } = useI18n();
  const { data: status, isLoading, isError } = useComplianceStatus();

  const columns: ColumnsType<BackupComplianceListItemDto> = useMemo(
    () => [
      {
        title: t("backupDr.compliance.columns.date"),
        dataIndex: "date",
        render: (iso: string) => {
          const d = new Date(iso);
          return Number.isNaN(d.getTime()) ? "—" : d.toLocaleString(formatLocale);
        },
      },
      {
        title: t("backupDr.compliance.columns.tenant"),
        key: "tenant",
        render: (_: unknown, row) =>
          row.tenantName || row.tenantId || t("backupDr.manualRestore.fields.sharedDump"),
      },
      {
        title: t("backupDr.compliance.columns.status"),
        dataIndex: "status",
      },
      {
        title: t("backupDr.compliance.columns.compliant"),
        dataIndex: "compliant",
        render: (ok: boolean, row) => (
          <Tag color={ok ? "success" : "error"}>
            {ok
              ? t("backupDr.compliance.values.yes")
              : t("backupDr.compliance.values.no")}
            <Typography.Text type="secondary" style={{ marginLeft: 6, fontSize: 11 }}>
              ({t(reasonLabelKey(row.reason))})
            </Typography.Text>
          </Tag>
        ),
      },
    ],
    [formatLocale, t],
  );

  if (isLoading) return <Spin />;

  if (isError) {
    return (
      <Alert
        type="error"
        showIcon
        title={t("backupDr.compliance.loadFailed")}
      />
    );
  }

  const lastCheck = status?.lastCheckUtc
    ? new Date(status.lastCheckUtc).toLocaleString(formatLocale)
    : "—";

  return (
    <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t("backupDr.compliance.disclaimerTitle")}
        description={
          status?.disclaimer || t("backupDr.compliance.disclaimerDefault")
        }
      />

      {status && !status.allCompliant && status.total > 0 ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          title={t("backupDr.compliance.warningTitle")}
          description={t("backupDr.compliance.warningDescription")}
        />
      ) : null}

      {status && status.allCompliant && status.total > 0 ? (
        <Alert
          type="success"
          showIcon
          style={{ marginBottom: 16 }}
          title={t("backupDr.compliance.allOkTitle")}
          description={t("backupDr.compliance.allOkDescription")}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.compliance.stats.total")}
              value={status?.total ?? 0}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.compliance.stats.compliant")}
              value={status?.compliant ?? 0}
              valueStyle={{ color: "#16a34a" }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.compliance.stats.nonCompliant")}
              value={status?.nonCompliant ?? 0}
              valueStyle={{ color: "#dc2626" }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.compliance.stats.lastCheck")}
              value={lastCheck}
            />
          </Card>
        </Col>
      </Row>

      {(status?.restoreRequestsTotal ?? 0) > 0 ? (
        <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
          <Col xs={24} sm={8}>
            <Card size="small">
              <Statistic
                title={t("backupDr.compliance.stats.restoreTotal")}
                value={status?.restoreRequestsTotal ?? 0}
              />
            </Card>
          </Col>
          <Col xs={24} sm={8}>
            <Card size="small">
              <Statistic
                title={t("backupDr.compliance.stats.restoreCompleted")}
                value={status?.restoreRequestsCompleted ?? 0}
              />
            </Card>
          </Col>
          <Col xs={24} sm={8}>
            <Card size="small">
              <Statistic
                title={t("backupDr.compliance.stats.restoreFailed")}
                value={status?.restoreRequestsFailed ?? 0}
              />
            </Card>
          </Col>
        </Row>
      ) : null}

      <Card
        size="small"
        title={t("backupDr.compliance.listTitle")}
        style={{ marginTop: 16 }}
      >
        <Table<BackupComplianceListItemDto>
          rowKey="backupRunId"
          size="small"
          dataSource={status?.backups ?? []}
          columns={columns}
          pagination={{ pageSize: 20, showSizeChanger: false }}
          locale={{ emptyText: t("backupDr.compliance.empty") }}
        />
      </Card>
    </>
  );
}
