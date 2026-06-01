"use client";

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Wiederherstellbarkeit: RPO/RTO istatistikleri, drill özeti, manuel restore drill tetikleme.
 */

import React, { useMemo } from "react";
import { Badge, Button, Card, Col, Divider, Progress, Row, Statistic, Typography } from 'antd';
import { useQueryClient } from "@tanstack/react-query";
import { getGetApiAdminBackupRecoverabilitySummaryQueryKey } from "@/api/generated/admin-backup/admin-backup";
import {
  getGetApiAdminRestoreVerificationReadinessQueryKey,
  getGetApiAdminRestoreVerificationRunsLatestQueryKey,
  usePostApiAdminRestoreVerificationTrigger,
} from "@/api/generated/admin-restore-verification/admin-restore-verification";
import { RestoreVerificationTriggerOrchestrationState } from "@/api/generated/model";
import { RestoreVerificationStatus } from "@/api/generated/model/restoreVerificationStatus";
import type {
  BackupRecoverabilitySummaryResponseDto,
  RestoreVerificationReadinessResponseDto,
  RestoreVerificationRunResponseDto,
} from "@/api/generated/model";
import { triggerErrorMessageBackupDashboard } from "@/features/backup-dr/logic/backupManualTriggerMessaging";
import {
  buildRestoreReadinessViewModel,
  metricStatusToProgressStatus,
  metricStatusToStatisticColor,
} from "@/features/backup-dr/logic/restoreReadinessPresentation";

export interface BackupRestoreReadinessCardProps {
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  restoreReadiness?: RestoreVerificationReadinessResponseDto | undefined;
  averageSucceededBackupDurationSeconds?: number | null;
  restoreStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  canManage?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function formatMetricNumber(value: number | null, fractionDigits = 1): string {
  if (value === null || Number.isNaN(value)) return "—";
  return value < 10 ? value.toFixed(fractionDigits) : String(Math.round(value));
}

export function BackupRestoreReadinessCard({
  recoverability,
  restoreLatest,
  averageSucceededBackupDurationSeconds,
  restoreStatusLabel,
  formatDt,
  formatLocale,
  canManage = false,
  t,
}: BackupRestoreReadinessCardProps) {
  const { message } = useAntdApp();

  const queryClient = useQueryClient();

  const model = useMemo(
    () =>
      buildRestoreReadinessViewModel({
        recoverability,
        restoreLatest,
        averageSucceededBackupDurationSeconds,
      }),
    [recoverability, restoreLatest, averageSucceededBackupDurationSeconds],
  );

  const restoreTrigger = usePostApiAdminRestoreVerificationTrigger({
    mutation: {
      onSuccess: async (res) => {
        if (res.newQueuedRunCreated) {
          message.success(t("backupDr.messages.restoreDrillEnqueued"));
        } else if (res.existingRunReturned) {
          if (
            res.orchestrationState === RestoreVerificationTriggerOrchestrationState.NUMBER_1
          ) {
            message.info(t("backupDr.messages.restoreDrillIdempotent"));
          } else {
            message.info(t("backupDr.messages.restoreDrillExistingActive"));
          }
        }
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminRestoreVerificationRunsLatestQueryKey(),
        });
        await queryClient.invalidateQueries({
          queryKey: ["/api/admin/restore-verification/runs"],
        });
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
        });
        await queryClient.invalidateQueries({
          queryKey: getGetApiAdminRestoreVerificationReadinessQueryKey(),
        });
      },
      onError: (err) => message.error(triggerErrorMessageBackupDashboard(err, t)),
    },
  });

  const drillStatus = restoreLatest?.status;

  return (
    <Card size="small" title={t("backupDr.monitoring.restoreReadiness.title")}>
      <Row gutter={16}>
        <Col xs={24} sm={12}>
          <Statistic
            title={t("backupDr.monitoring.restoreReadiness.rpoTitle")}
            value={formatMetricNumber(model.rpoHours)}
            suffix={model.rpoHours !== null ? t("backupDr.monitoring.restoreReadiness.hoursSuffix") : undefined}
            styles={{ content: {  color: metricStatusToStatisticColor(model.rpoStatus)  } }}
          />
          <Progress
            percent={Math.round(model.rpoProgressPercent)}
            status={metricStatusToProgressStatus(model.rpoStatus)}
            showInfo={false}
            style={{ marginTop: 8 }}
          />
        </Col>
        <Col xs={24} sm={12}>
          <Statistic
            title={t("backupDr.monitoring.restoreReadiness.rtoTitle")}
            value={formatMetricNumber(model.rtoMinutes)}
            suffix={
              model.rtoMinutes !== null
                ? t("backupDr.monitoring.restoreReadiness.minutesSuffix")
                : undefined
            }
            styles={{ content: {  color: metricStatusToStatisticColor(model.rtoStatus)  } }}
          />
          <Progress
            percent={Math.round(model.rtoProgressPercent)}
            status={metricStatusToProgressStatus(model.rtoStatus)}
            showInfo={false}
            style={{ marginTop: 8 }}
          />
        </Col>
      </Row>

      <Divider style={{ margin: "12px 0" }} />

      <div>
        <Typography.Paragraph style={{ marginBottom: 8 }}>
          <Typography.Text strong>
            {t("backupDr.monitoring.restoreReadiness.lastSuccessfulDrill")}
          </Typography.Text>{" "}
          {formatDt(model.lastSuccessfulDrillAt, formatLocale)}
        </Typography.Paragraph>
        <Typography.Paragraph style={{ marginBottom: 8 }}>
          <Typography.Text strong>
            {t("backupDr.monitoring.restoreReadiness.drillStatus")}
          </Typography.Text>{" "}
          <Badge status={model.drillBadgeStatus} />
          {restoreStatusLabel(drillStatus)}
        </Typography.Paragraph>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          <Typography.Text strong>
            {t("backupDr.monitoring.restoreReadiness.lastVerifiedBackup")}
          </Typography.Text>{" "}
          {formatDt(model.lastVerifiedBackupAt, formatLocale)}
        </Typography.Paragraph>
        {drillStatus === RestoreVerificationStatus.NUMBER_3 &&
        restoreLatest?.failureDetail ? (
          <Typography.Paragraph type="danger" style={{ marginTop: 8, marginBottom: 0 }}>
            {restoreLatest.failureDetail}
          </Typography.Paragraph>
        ) : null}
      </div>

      {canManage ? (
        <Button
          type="primary"
          loading={restoreTrigger.isPending}
          onClick={() => restoreTrigger.mutate({ data: {} })}
          style={{ marginTop: 16 }}
        >
          {t("backupDr.monitoring.restoreReadiness.triggerDrill")}
        </Button>
      ) : null}
    </Card>
  );
}

/** @deprecated Use `BackupRestoreReadinessCard`. */
export const RestoreReadinessCard = BackupRestoreReadinessCard;
