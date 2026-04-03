"use client";

/**
 * Son kanıt özeti: dört zaman damgası — üstte özet, ayrıntı drill-down’da.
 * `grouped`: tek kartta “son deneme” vs “kanıt çapaları” ayrımı (daha az rekabet).
 */

import React from "react";
import { Card, Col, Descriptions, Divider, Row, Tag, Typography } from "antd";
import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  RestoreVerificationRunResponseDto,
} from "@/api/generated/model";
import {
  mapBackupRunStatusAntdColor,
  mapRestoreVerificationStatusAntdColor,
} from "@/features/backup-dr/logic/backupDrMappers";
import { formatRecoverabilityTimestampOrProofGap } from "@/features/backup-dr/logic/backupDrOperatorTruth";

export interface BackupDrRecentEvidenceGridProps {
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  latestRun: BackupRunResponseDto | undefined;
  backupStatusLabel: (status: number | undefined) => string;
  restoreStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  t: (key: string, options?: Record<string, string | number>) => string;
  /** `grid`: dört ayrı kart; `grouped`: iki grup, tek kart (yoğunluk). */
  layout?: "grid" | "grouped";
}

export function BackupDrRecentEvidenceGrid({
  recoverability,
  restoreLatest,
  latestRun,
  backupStatusLabel,
  restoreStatusLabel,
  formatDt,
  formatLocale,
  t,
  layout = "grid",
}: BackupDrRecentEvidenceGridProps) {
  if (layout === "grouped") {
    return (
      <Card
        size="small"
        title={t("backupDr.ia.recentEvidence")}
        styles={{ body: { paddingTop: 12 } }}
      >
        <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
          {t("backupDr.layout.groupLatestAttempt")}
        </Typography.Text>
        <Row gutter={[12, 12]}>
          <Col xs={24} md={12}>
            {!latestRun?.id ? (
              <Typography.Text type="secondary">—</Typography.Text>
            ) : (
              <Descriptions column={1} size="small">
                <Descriptions.Item
                  label={t("backupDr.confidenceDashboard.labels.runId")}
                >
                  <Typography.Text code copyable={{ text: latestRun.id }}>
                    {latestRun.id}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t("backupDr.table.status")}>
                  <Tag color={mapBackupRunStatusAntdColor(latestRun.status)}>
                    {backupStatusLabel(latestRun.status)}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t("backupDr.latestRun.completed")}>
                  {formatDt(latestRun.completedAt, formatLocale)}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Col>
          <Col xs={24} md={12}>
            {!restoreLatest ? (
              <Typography.Text type="secondary">—</Typography.Text>
            ) : (
              <Descriptions column={1} size="small">
                <Descriptions.Item label={t("backupDr.table.status")}>
                  <Tag
                    color={mapRestoreVerificationStatusAntdColor(
                      restoreLatest.status,
                    )}
                  >
                    {restoreStatusLabel(restoreLatest.status)}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t("backupDr.latestRun.completed")}>
                  {formatDt(restoreLatest.completedAt, formatLocale)}
                </Descriptions.Item>
              </Descriptions>
            )}
          </Col>
        </Row>
        <Divider style={{ margin: "12px 0" }} />
        <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
          {t("backupDr.layout.groupProofAnchors")}
        </Typography.Text>
        <Row gutter={[12, 12]}>
          <Col xs={24} md={12}>
            <Descriptions column={1} size="small">
              <Descriptions.Item
                label={t("backupDr.recentEvidence.artifactVerificationAt")}
              >
                <Typography.Text strong>
                  {formatRecoverabilityTimestampOrProofGap(
                    recoverability?.lastSuccessfulArtifactVerificationAt,
                    formatDt,
                    formatLocale,
                    t,
                  )}
                </Typography.Text>
              </Descriptions.Item>
            </Descriptions>
            <Typography.Paragraph
              type="secondary"
              style={{ marginBottom: 0, marginTop: 8, fontSize: 12 }}
            >
              {t("backupDr.recentEvidence.artifactVerificationScope")}
            </Typography.Paragraph>
          </Col>
          <Col xs={24} md={12}>
            <Descriptions column={1} size="small">
              <Descriptions.Item
                label={
                  recoverability?.lastSuccessfulBackupRunIsSimulatedExecution ===
                  true
                    ? t("backupDr.recoverability.proofBlock.backupStub")
                    : t("backupDr.recentEvidence.lkgTimestampLabel")
                }
              >
                <Typography.Text strong>
                  {formatRecoverabilityTimestampOrProofGap(
                    recoverability?.lastSuccessfulBackupAt,
                    formatDt,
                    formatLocale,
                    t,
                  )}
                </Typography.Text>
              </Descriptions.Item>
              {recoverability?.lastSuccessfulBackupRunId ? (
                <Descriptions.Item
                  label={t("backupDr.confidenceDashboard.labels.runId")}
                >
                  <Typography.Text
                    code
                    copyable={{ text: recoverability.lastSuccessfulBackupRunId }}
                  >
                    {recoverability.lastSuccessfulBackupRunId}
                  </Typography.Text>
                </Descriptions.Item>
              ) : null}
              {recoverability?.lastSuccessfulBackupRunIsSimulatedExecution ===
              true ? (
                <Descriptions.Item label={t("backupDr.table.adapter")}>
                  <Tag>{t("backupDr.recentEvidence.simulatedExecution")}</Tag>
                </Descriptions.Item>
              ) : null}
            </Descriptions>
          </Col>
        </Row>
      </Card>
    );
  }

  return (
    <Row gutter={[12, 12]}>
      <Col xs={24} sm={12} xl={6}>
        <Card
          size="small"
          title={t("backupDr.ia.columns.latestBackupRun")}
          styles={{ body: { paddingTop: 12 } }}
        >
          {!latestRun?.id ? (
            <Typography.Text type="secondary">—</Typography.Text>
          ) : (
            <Descriptions column={1} size="small">
              <Descriptions.Item
                label={t("backupDr.confidenceDashboard.labels.runId")}
              >
                <Typography.Text code copyable={{ text: latestRun.id }}>
                  {latestRun.id}
                </Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.table.status")}>
                <Tag color={mapBackupRunStatusAntdColor(latestRun.status)}>
                  {backupStatusLabel(latestRun.status)}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.latestRun.completed")}>
                {formatDt(latestRun.completedAt, formatLocale)}
              </Descriptions.Item>
            </Descriptions>
          )}
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card
          size="small"
          title={t("backupDr.ia.columns.latestRealArtifact")}
          styles={{ body: { paddingTop: 12 } }}
        >
          <Descriptions column={1} size="small">
            <Descriptions.Item
              label={t("backupDr.recentEvidence.artifactVerificationAt")}
            >
              <Typography.Text strong>
                {formatRecoverabilityTimestampOrProofGap(
                  recoverability?.lastSuccessfulArtifactVerificationAt,
                  formatDt,
                  formatLocale,
                  t,
                )}
              </Typography.Text>
            </Descriptions.Item>
          </Descriptions>
          <Typography.Paragraph
            type="secondary"
            style={{ marginBottom: 0, marginTop: 8, fontSize: 12 }}
          >
            {t("backupDr.recentEvidence.artifactVerificationScope")}
          </Typography.Paragraph>
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card
          size="small"
          title={t("backupDr.ia.columns.latestRestoreDrill")}
          styles={{ body: { paddingTop: 12 } }}
        >
          {!restoreLatest ? (
            <Typography.Text type="secondary">—</Typography.Text>
          ) : (
            <Descriptions column={1} size="small">
              <Descriptions.Item label={t("backupDr.table.status")}>
                <Tag
                  color={mapRestoreVerificationStatusAntdColor(
                    restoreLatest.status,
                  )}
                >
                  {restoreStatusLabel(restoreLatest.status)}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t("backupDr.latestRun.completed")}>
                {formatDt(restoreLatest.completedAt, formatLocale)}
              </Descriptions.Item>
            </Descriptions>
          )}
        </Card>
      </Col>
      <Col xs={24} sm={12} xl={6}>
        <Card
          size="small"
          title={t("backupDr.ia.columns.lkgProof")}
          styles={{ body: { paddingTop: 12 } }}
        >
          <Descriptions column={1} size="small">
            <Descriptions.Item
              label={
                recoverability?.lastSuccessfulBackupRunIsSimulatedExecution ===
                true
                  ? t("backupDr.recoverability.proofBlock.backupStub")
                  : t("backupDr.recentEvidence.lkgTimestampLabel")
              }
            >
              <Typography.Text strong>
                {formatRecoverabilityTimestampOrProofGap(
                  recoverability?.lastSuccessfulBackupAt,
                  formatDt,
                  formatLocale,
                  t,
                )}
              </Typography.Text>
            </Descriptions.Item>
            {recoverability?.lastSuccessfulBackupRunId ? (
              <Descriptions.Item
                label={t("backupDr.confidenceDashboard.labels.runId")}
              >
                <Typography.Text
                  code
                  copyable={{ text: recoverability.lastSuccessfulBackupRunId }}
                >
                  {recoverability.lastSuccessfulBackupRunId}
                </Typography.Text>
              </Descriptions.Item>
            ) : null}
            {recoverability?.lastSuccessfulBackupRunIsSimulatedExecution ===
            true ? (
              <Descriptions.Item label={t("backupDr.table.adapter")}>
                <Tag>{t("backupDr.recentEvidence.simulatedExecution")}</Tag>
              </Descriptions.Item>
            ) : null}
          </Descriptions>
        </Card>
      </Col>
    </Row>
  );
}
