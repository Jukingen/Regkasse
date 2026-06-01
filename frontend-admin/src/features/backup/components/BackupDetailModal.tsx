"use client";

/**
 * Backup run detail modal — GET /api/admin/backup/runs/{id} (overview, artifacts, verification, pipeline, logs).
 */

import React, { useCallback, useMemo, useState } from "react";
import {
  Alert,
  Button,
  Collapse,
  Descriptions,
  Divider,
  Modal,
  Progress,
  Space,
  Spin,
  Table,
  Tabs,
  Timeline,
  Typography,
} from "antd";
import { DownloadOutlined } from "@ant-design/icons";
import type { ColumnsType } from "antd/es/table";
import type {
  BackupArtifactResponseDto,
  BackupVerificationResponseDto,
} from "@/api/generated/model";
import { useI18n } from "@/i18n";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
import { useBackupRun } from "@/features/backup/hooks/useBackupRun";
import { BackupArtifactsDownloadCard } from "@/features/backup-dr/components/BackupArtifactsDownloadCard";
import {
  resolveBackupPipelineStepsForUi,
} from "@/features/backup-dr/logic/backupPipelineDerived";
import { isBackupPipelineClientFallbackEnabled } from "@/features/backup-dr/logic/backupPipelineEnv";
import { isSimulatedBackupAdapterKind } from "@/features/backup-dr/logic/backupDrMappers";
import { formatBackupBytes } from "@/features/backup-dr/logic/backupFormat";
import { downloadBackupArtifactFile } from "@/features/backup-dr/logic/downloadBackupArtifactFile";
import { BackupStatusBadge } from "@/features/backup/components/BackupStatusBadge";
import {
  backupTriggerSourceLabelKey,
  isBackupRunSucceeded,
  pipelineStepTimelineColor,
} from "@/features/backup/logic/backupRunDetailPresentation";
import {
  resolveBackupRunDurationLabel,
  resolveBackupRunSizeLabel,
  resolveBackupRunTotalBytes,
} from "@/features/backup/logic/backupRunTablePresentation";
import { BackupVerificationReportPanel } from "@/features/backup/components/BackupVerificationReportPanel";

export interface BackupDetailModalProps {
  runId: string | null;
  open: boolean;
  onClose: () => void;
}

export function BackupDetailModal({ runId, open, onClose }: BackupDetailModalProps) {
  const { t, formatLocale } = useI18n();
  const { canConfigure: canManage } = useBackupPermissions();
  const [downloading, setDownloading] = useState(false);

  const { data: run, isLoading, isError, isFetching } = useBackupRun(runId, {
    enabled: open,
  });

  const allowClientPipelineFallback = isBackupPipelineClientFallbackEnabled();
  const simulated = isSimulatedBackupAdapterKind(run?.adapterKind);

  const pipelineResolved = useMemo(
    () =>
      resolveBackupPipelineStepsForUi(run ?? null, run ?? null, undefined, {
        allowClientFallback: allowClientPipelineFallback,
      }),
    [allowClientPipelineFallback, run],
  );

  const formatDateTime = useCallback(
    (iso: string | undefined | null) => {
      if (!iso) return "—";
      try {
        return new Date(iso).toLocaleString(formatLocale);
      } catch {
        return iso;
      }
    },
    [formatLocale],
  );

  const primaryDownloadArtifact = useMemo(() => {
    const arts = run?.artifacts ?? [];
    return (
      arts.find((a) => a.isFilePresentForDownload && a.id) ??
      arts.find((a) => a.id) ??
      null
    );
  }, [run?.artifacts]);

  const handleDownloadPrimary = useCallback(async () => {
    if (!run?.id || !primaryDownloadArtifact?.id) return;
    setDownloading(true);
    try {
      const fallback = `backup-${run.id}-${primaryDownloadArtifact.id}`;
      await downloadBackupArtifactFile(run.id, primaryDownloadArtifact.id, fallback);
    } finally {
      setDownloading(false);
    }
  }, [primaryDownloadArtifact?.id, run?.id]);

  const artifactColumns: ColumnsType<BackupArtifactResponseDto> = useMemo(
    () => [
      {
        title: t("backupDr.detailModal.artifacts.type"),
        key: "type",
        render: (_: unknown, row: BackupArtifactResponseDto) => {
          const key = `backupDr.runsTable.artifactType.${row.artifactType ?? ""}`;
          const label = t(key);
          return label === key ? String(row.artifactType ?? "—") : label;
        },
      },
      {
        title: t("backupDr.detailModal.artifacts.size"),
        key: "size",
        render: (_: unknown, row: BackupArtifactResponseDto) => {
          if (row.formattedSize?.trim()) return row.formattedSize.trim();
          const label = formatBackupBytes(row.byteSize ?? undefined, t);
          const bytes = row.byteSize ?? 0;
          if (bytes > 0) {
            return (
              <span>
                {label}{" "}
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  ({bytes.toLocaleString(formatLocale)})
                </Typography.Text>
              </span>
            );
          }
          return label;
        },
      },
      {
        title: t("backupDr.detailModal.artifacts.createdAt"),
        dataIndex: "createdAt",
        key: "createdAt",
        render: (v: string | undefined) => formatDateTime(v),
      },
      {
        title: t("backupDr.detailModal.artifacts.lifecycle"),
        dataIndex: "lifecycleState",
        key: "lifecycle",
        render: (v: number | undefined) => {
          if (v === undefined) return "—";
          const key = `backupDr.lifecycle.${v}`;
          const label = t(key);
          return label === key ? String(v) : label;
        },
      },
      {
        title: t("backupDr.detailModal.artifacts.onDisk"),
        key: "onDisk",
        render: (_: unknown, row: BackupArtifactResponseDto) =>
          row.isFilePresentForDownload
            ? t("backupDr.monitoring.configHealth.yes")
            : t("backupDr.monitoring.configHealth.no"),
      },
      {
        title: t("backupDr.runsTable.actions"),
        key: "actions",
        render: (_: unknown, row: BackupArtifactResponseDto) =>
          run?.id && row.id && row.isFilePresentForDownload && canManage ? (
            <Button
              type="link"
              size="small"
              icon={<DownloadOutlined />}
              onClick={() =>
                void downloadBackupArtifactFile(
                  run.id!,
                  row.id!,
                  `backup-${run.id}-${row.id}`,
                )
              }
            >
              {t("backupDr.detailModal.download")}
            </Button>
          ) : (
            "—"
          ),
      },
    ],
    [canManage, formatDateTime, formatLocale, run?.id, t],
  );

  const renderOverviewMetrics = useCallback(() => {
    if (!run) return null;

    const durationLabel = resolveBackupRunDurationLabel(run, t);
    const sizeLabel = resolveBackupRunSizeLabel(run, t);
    const totalBytes = resolveBackupRunTotalBytes(run);
    const compression =
      run.compressionRatio != null && !Number.isNaN(run.compressionRatio)
        ? Math.round(run.compressionRatio)
        : null;

    return (
      <Descriptions bordered column={2} size="small">
        <Descriptions.Item label={t("backupDr.runsTable.statusColumn")}>
          <BackupStatusBadge status={run.status} />
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.detailModal.triggerLabel")}>
          {t(backupTriggerSourceLabelKey(run.triggerSource))}
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.detailModal.startedAt")}>
          {formatDateTime(run.startedAt)}
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.detailModal.completedAt")}>
          {formatDateTime(run.completedAt)}
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.runsTable.duration")}>
          <Typography.Text strong>{durationLabel}</Typography.Text>
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.detailModal.requestedAt")}>
          {formatDateTime(run.requestedAt)}
        </Descriptions.Item>
        <Descriptions.Item label={t("backupDr.detailModal.totalSize")} span={2}>
          <Typography.Text strong>{sizeLabel}</Typography.Text>
          {totalBytes > 0 ? (
            <Typography.Text type="secondary">
              {" "}
              {t("backupDr.detailModal.totalSizeBytes", {
                bytes: totalBytes.toLocaleString(formatLocale),
              })}
            </Typography.Text>
          ) : null}
        </Descriptions.Item>
        {compression != null ? (
          <Descriptions.Item label={t("backupDr.detailModal.compression")} span={2}>
            <Progress
              percent={compression}
              size="small"
              status={compression < 50 ? "success" : "normal"}
              format={() =>
                t("backupDr.detailModal.compressionPercent", { percent: compression })
              }
            />
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item label={t("backupDr.table.adapter")}>
          {run.adapterKind ?? "—"}
        </Descriptions.Item>
        {run.failureDetail || run.failureCode ? (
          <Descriptions.Item label={t("backupDr.runsTable.error")} span={2}>
            <Typography.Text type="danger">
              {[run.failureCode, run.failureDetail].filter(Boolean).join(" — ")}
            </Typography.Text>
          </Descriptions.Item>
        ) : null}
      </Descriptions>
    );
  }, [formatDateTime, formatLocale, run, t]);

  const renderArtifactsBreakdown = useCallback(() => {
    if (!run) return null;
    return (
      <>
        <Divider orientation="left">{t("backupDr.detailModal.artifactsSection")}</Divider>
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t("backupDr.detailModal.artifacts.sizeBreakdownHint")}
        </Typography.Paragraph>
        <Table<BackupArtifactResponseDto>
          rowKey={(r) => r.id ?? String(r.artifactType)}
          size="small"
          pagination={false}
          dataSource={run.artifacts ?? []}
          columns={artifactColumns}
          locale={{ emptyText: t("backupDr.detailModal.artifacts.empty") }}
        />
      </>
    );
  }, [artifactColumns, run, t]);

  const verificationItems = useMemo(() => {
    const list = run?.verifications ?? [];
    if (!list.length) {
      return (
        <Typography.Text type="secondary">{t("backupDr.detailModal.verification.empty")}</Typography.Text>
      );
    }
    return (
      <Collapse
        items={list.map((v: BackupVerificationResponseDto) => ({
          key: v.id ?? `${v.startedAt}-${v.status}`,
          label: `${t("backupDr.detailModal.verification.panelTitle")}: ${verificationStatusLabel(v.status, t)}`,
          children: (
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={t("backupDr.detailModal.verification.started")}>
                {formatDateTime(v.startedAt)}
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.detailModal.verification.completed")}>
                {formatDateTime(v.completedAt)}
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.detailModal.verification.verifier")}>
                {v.verifierSource ?? "—"}
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.detailModal.verification.completeness")}>
                {v.completenessFlag
                  ? t("backupDr.detailModal.verification.completenessOk")
                  : t("backupDr.detailModal.verification.completenessMissing")}
              </Descriptions.Item>
              {v.failureReason ? (
                <Descriptions.Item label={t("backupDr.runsTable.error")}>
                  <Typography.Text type="danger">{v.failureReason}</Typography.Text>
                </Descriptions.Item>
              ) : null}
            </Descriptions>
          ),
        }))}
      />
    );
  }, [formatDateTime, run?.verifications, t]);

  const tabItems = useMemo(() => {
    if (!run) return [];

    return [
      {
        key: "overview",
        label: t("backupDr.detailModal.tabs.overview"),
        children: (
          <>
            {simulated ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                title={t("backupDr.management.runDetail.simulatedHint")}
              />
            ) : null}
            {renderOverviewMetrics()}
            {renderArtifactsBreakdown()}
          </>
        ),
      },
      {
        key: "artifacts",
        label: t("backupDr.detailModal.tabs.artifacts"),
        children: (
          <Space orientation="vertical" style={{ width: "100%" }} size="middle">
            {renderArtifactsBreakdown()}
            {run.id && (run.artifacts?.length ?? 0) > 0 ? (
              <BackupArtifactsDownloadCard
                variant="latest_success"
                runId={run.id}
                artifacts={run.artifacts ?? []}
                canManage={canManage}
                isSimulatedExecution={simulated}
                runAdapterKind={run.adapterKind}
                simulatedOperationalMode={simulated}
                loadingArtifacts={isFetching}
                t={t}
              />
            ) : null}
          </Space>
        ),
      },
      {
        key: "verification",
        label: t("backupDr.detailModal.tabs.verification"),
        children: (
          <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
            {verificationItems}
            {run.id && isBackupRunSucceeded(run.status) ? (
              <>
                <Divider orientation="left">{t("backupDr.verificationReport.sectionTitle")}</Divider>
                <BackupVerificationReportPanel runId={run.id} enabled={open} />
              </>
            ) : null}
          </Space>
        ),
      },
      {
        key: "pipeline",
        label: t("backupDr.detailModal.tabs.pipeline"),
        children:
          pipelineResolved.steps.length > 0 ? (
            <Timeline
              items={pipelineResolved.steps.map((step) => ({
                color: pipelineStepTimelineColor(step.state),
                children: (
                  <div>
                    <Typography.Text strong>{t(step.titleKey)}</Typography.Text>
                    <div>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {t(`backupDr.pipelineSteps.state.${step.state}`)}
                      </Typography.Text>
                    </div>
                    {step.hintKey ? (
                      <Typography.Paragraph
                        type="secondary"
                        style={{ marginBottom: 0, fontSize: 12 }}
                      >
                        {t(step.hintKey)}
                      </Typography.Paragraph>
                    ) : null}
                  </div>
                ),
              }))}
            />
          ) : (
            <Typography.Text type="secondary">{t("backupDr.detailModal.pipeline.empty")}</Typography.Text>
          ),
      },
      {
        key: "logs",
        label: t("backupDr.detailModal.tabs.logs"),
        children: (
          <pre
            style={{
              background: "#f5f5f5",
              padding: 16,
              borderRadius: 8,
              fontSize: 11,
              overflow: "auto",
              maxHeight: 384,
              margin: 0,
            }}
          >
            {JSON.stringify(run, null, 2)}
          </pre>
        ),
      },
    ];
  }, [
    artifactColumns,
    canManage,
    formatDateTime,
    isFetching,
    pipelineResolved.steps,
    renderArtifactsBreakdown,
    renderOverviewMetrics,
    run,
    simulated,
    t,
    verificationItems,
  ]);

  const showDownloadFooter =
    isBackupRunSucceeded(run?.status) &&
    Boolean(run?.id && primaryDownloadArtifact?.id && canManage);

  return (
    <Modal
      title={
        run?.id
          ? t("backupDr.detailModal.title", { id: run.id })
          : t("backupDr.detailModal.titleLoading")
      }
      open={open}
      onCancel={onClose}
      width={700}
      destroyOnHidden
      footer={
        <Space>
          <Button onClick={onClose}>{t("backupDr.detailModal.close")}</Button>
          {showDownloadFooter ? (
            <Button type="primary" loading={downloading} onClick={() => void handleDownloadPrimary()}>
              {t("backupDr.detailModal.downloadBackup")}
            </Button>
          ) : null}
        </Space>
      }
    >
      {!runId ? null : isLoading && !run ? (
        <Spin />
      ) : isError ? (
        <Alert type="error" showIcon title={t("backupDr.errors.runDetailPartial")} />
      ) : !run ? (
        <Alert type="warning" showIcon title={t("backupDr.management.runDetail.notFound")} />
      ) : (
        <Tabs items={tabItems} />
      )}
    </Modal>
  );
}

function verificationStatusLabel(
  status: number | undefined,
  t: (key: string) => string,
): string {
  if (status === undefined) return t("backupDr.summary.unknown");
  const key = `backupDr.verificationStatus.${status}`;
  const label = t(key);
  return label === key ? String(status) : label;
}
