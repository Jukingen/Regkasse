"use client";

/**
 * Backup & DR panosu: salt görünürlük + yalnızca API kuyruğa alma; pipeline adımları DTO’dan türetilir.
 * Kompakt düzen: üstte tek kanıt bloğu, gruplu zaman damgası, ortam uyarıları isteğe bağlı daraltılabilir.
 */

import React, { useCallback, useEffect, useMemo, useRef } from "react";
import {
  Alert,
  Button,
  Card,
  Col,
  Collapse,
  Descriptions,
  Divider,
  Row,
  Space,
  Spin,
  Statistic,
  Tag,
  Tooltip,
  Typography,
} from "antd";
import { ReloadOutlined } from "@ant-design/icons";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useI18n } from "@/i18n";
import { hasPermission, PERMISSIONS } from "@/shared/auth/permissions";
import { useAuth } from "@/features/auth/hooks/useAuth";
import {
  getGetApiAdminBackupRecoverabilitySummaryQueryKey,
  getGetApiAdminBackupRunsIdQueryKey,
  getGetApiAdminBackupVerificationLatestQueryKey,
  useGetApiAdminBackupRecoverabilitySummary,
  useGetApiAdminBackupRuns,
  useGetApiAdminBackupStatusLatest,
  useGetApiAdminBackupVerificationLatest,
  useGetApiAdminBackupRunsId,
} from "@/api/generated/admin-backup/admin-backup";
import {
  useGetApiAdminRestoreVerificationReadiness,
  useGetApiAdminRestoreVerificationRunsLatest,
} from "@/api/generated/admin-restore-verification/admin-restore-verification";
import {
  BackupRunResponseDto,
  BackupRunResponseDtoStatus,
} from "@/api/generated/model";
import {
  healthStatisticValueStyle,
  restoreReadinessStatisticValueStyle,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapDumpInspectionTriState,
  mapRestoreVerificationStatusAntdColor,
  normalizeHealthLevelString,
  isSimulatedBackupAdapterKind,
} from "@/features/backup-dr/logic/backupDrMappers";
import {
  REAL_DUMP_PATH_BANNER_ALERT_TYPE,
  mapExternalCopyVariantToAlertType,
  mapOperatorValidityStripToAlertType,
} from "@/features/backup-dr/logic/backupDrGlancePresentation";
import {
  buildBackupOperatorTruthModel,
  tagColorForConfigurationHealthUiKind,
} from "@/features/backup-dr/logic/backupDrOperatorTruthModel";
import {
  buildDrProofPresentationModel,
  buildDrProofScanTags,
} from "@/features/backup-dr/logic/drProofLevelPresentation";
import { extendRestoreVerificationRunDto } from "@/features/backup-dr/logic/backupDrRestoreRunDtoCompat";
import { BackupArtifactsDownloadCard } from "@/features/backup-dr/components/BackupArtifactsDownloadCard";
import { BackupRunProgressBanner } from "@/features/backup-dr/components/BackupRunProgressBanner";
import { BackupRecoverabilityCard } from "@/features/backup-dr/components/BackupRecoverabilityCard";
import { BackupManualActionsPanel } from "@/features/backup-dr/components/BackupManualActionsPanel";
import { BackupRecentRestoreDrillsTable } from "@/features/backup-dr/components/BackupRecentRestoreDrillsTable";
import { BackupRecentRunsTable } from "@/features/backup-dr/components/BackupRecentRunsTable";
import { RestoreRequestModal } from "@/features/backup-dr/components/RestoreRequestModal";
import { RestoreApprovalModal } from "@/features/backup-dr/components/RestoreApprovalModal";
import { ManualRestoreRequestsTable } from "@/features/backup-dr/components/ManualRestoreRequestsTable";
import { isSuperAdmin } from "@/features/auth/constants/roles";
import { BackupStatusCard } from "@/features/backup-dr/components/BackupStatusCard";
import { HealthBanner } from "@/features/backup-dr/components/HealthBanner";
import { RestoreVerificationCard } from "@/features/backup-dr/components/RestoreVerificationCard";
import { BackupDrPostureSummary } from "@/features/backup-dr/components/BackupDrPostureSummary";
import { BackupMonitoringSection } from "@/features/backup-dr/components/BackupMonitoringSection";
import { BackupDrDataFreshnessStrip } from "@/features/backup-dr/components/BackupDrDataFreshnessStrip";
import { BackupDrEvidenceSurface } from "@/features/backup-dr/components/BackupDrEvidenceSurface";
import { BackupDrRecentEvidenceGrid } from "@/features/backup-dr/components/BackupDrRecentEvidenceGrid";
import { BackupDrSection } from "@/features/backup-dr/components/BackupDrSection";
import { BackupExecutionModeCard } from "@/features/backup-dr/components/BackupExecutionModeCard";
import { BackupScheduleSettings } from "@/features/backup-dr/components/BackupScheduleSettings";
import {
  getBackupExecutionMode,
  getGetApiAdminBackupExecutionModeQueryKey,
} from "@/features/backup-dr/logic/backupExecutionModeApi";
import { isBackupPipelineClientFallbackEnabled } from "@/features/backup-dr/logic/backupPipelineEnv";
import { apiNullableToUndefined } from "@/features/backup-dr/logic/backupDrDtoNormalize";
import { shouldOfferLastKnownGoodArtifactDownload } from "@/features/backup-dr/logic/backupArtifactDownloadTruth";
import {
  BACKUP_RECENT_RUNS_PAGE_SIZE,
  usePollAlignedWithLatestDashboardBackup,
  usePollBackupLatestDashboardInterval,
  usePollRestoreVerificationDashboardInterval,
  usePollRunDetailDashboardInterval,
} from "@/features/backup-dr/logic/backupDashboardQueryTiming";
import { isBackupLatestRunActiveStatus } from "@/features/backup-dr/logic/backupRunDetailPollPolicy";

function formatDt(
  iso: string | undefined | null,
  formatLocale: string,
): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString(formatLocale);
  } catch {
    return iso;
  }
}

/** Depo kökünde geliştirici yedek kılavuzu — UI’da gösterilir (Orval DTO ile aynı kaynak fikri). */
const BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH =
  "backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md";

function backupDiagnosticTagColor(severity: string | null | undefined): string {
  const s = (severity ?? "").toLowerCase();
  if (s === "error") return "red";
  if (s === "warning") return "orange";
  return "blue";
}

export interface BackupDrDashboardProps {
  /** Sekmeli yönetim panelinde üst başlık/zaman planı tekrarını gizler. */
  embedded?: boolean;
  hideScheduleSettings?: boolean;
  onSelectBackupRun?: (run: BackupRunResponseDto) => void;
}

export function BackupDrDashboard({
  embedded = false,
  hideScheduleSettings = false,
  onSelectBackupRun,
}: BackupDrDashboardProps = {}) {
  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const canRequestManualRestore = isSuperAdmin(user?.role);
  const [manualRestoreRun, setManualRestoreRun] =
    React.useState<BackupRunResponseDto | null>(null);
  const [manualRestoreApprovalId, setManualRestoreApprovalId] =
    React.useState<string | null>(null);
  const allowClientPipelineFallback = isBackupPipelineClientFallbackEnabled();

  const executionModeQuery = useQuery({
    queryKey: getGetApiAdminBackupExecutionModeQueryKey(),
    queryFn: getBackupExecutionMode,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
  });

  const orchestrationRunsParams = useMemo(
    () => ({ page: 1, pageSize: BACKUP_RECENT_RUNS_PAGE_SIZE }),
    [],
  );

  const pollBackup = usePollBackupLatestDashboardInterval();

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: pollBackup, refetchOnWindowFocus: true },
  });

  const latest = apiNullableToUndefined(statusQuery.data?.latestRun);

  /** Latest-run ile aynı hızda: geçmiş / doğrulama / recoverability, yedek aktifken hizalanır. */
  const pollAlignedWithLatestBackup = usePollAlignedWithLatestDashboardBackup(latest?.status);

  /** Run-by-id parity (shared with `BackupStatusCard` observers for the same ids). */
  const pollRunDetail = usePollRunDetailDashboardInterval(latest?.id, latest?.status);

  /** First page kept on the shell for global loading parity (dedupes with table on page 1). */
  const runsOrchestrationQuery = useGetApiAdminBackupRuns(orchestrationRunsParams, {
    query: {
      refetchInterval: pollAlignedWithLatestBackup,
      refetchOnWindowFocus: true,
    },
  });
  const verificationQuery = useGetApiAdminBackupVerificationLatest({
    query: {
      refetchInterval: pollAlignedWithLatestBackup,
      refetchOnWindowFocus: true,
    },
  });
  const recoverabilityQuery = useGetApiAdminBackupRecoverabilitySummary({
    query: {
      refetchInterval: pollAlignedWithLatestBackup,
      refetchOnWindowFocus: true,
    },
  });
  const pollRestore = usePollRestoreVerificationDashboardInterval();
  const restoreLatestQuery = useGetApiAdminRestoreVerificationRunsLatest({
    query: { refetchInterval: pollRestore, refetchOnWindowFocus: true },
  });
  const restoreReadinessQuery = useGetApiAdminRestoreVerificationReadiness({
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });

  const verificationForTruth = apiNullableToUndefined(verificationQuery.data);
  const restoreLatestForTruth = apiNullableToUndefined(restoreLatestQuery.data);

  const policy = statusQuery.data?.artifactPipelinePolicy;
  const runDetailQuery = useGetApiAdminBackupRunsId(latest?.id ?? "", {
    query: {
      enabled: Boolean(latest?.id),
      refetchInterval: pollRunDetail,
      refetchOnWindowFocus: true,
    },
  });

  const lkgRunId =
    recoverabilityQuery.data?.lastSuccessfulBackupRunId?.trim() ?? "";
  const offerLastKnownGoodDownloads = useMemo(
    () =>
      shouldOfferLastKnownGoodArtifactDownload({
        latestRunId: latest?.id,
        latestStatus: latest?.status,
        lastSuccessfulBackupRunId:
          recoverabilityQuery.data?.lastSuccessfulBackupRunId,
      }),
    [
      latest?.id,
      latest?.status,
      recoverabilityQuery.data?.lastSuccessfulBackupRunId,
    ],
  );

  const lkgRunDetailQuery = useGetApiAdminBackupRunsId(lkgRunId, {
    query: {
      enabled: Boolean(lkgRunId && offerLastKnownGoodDownloads),
      refetchOnWindowFocus: true,
    },
  });

  const latestRunSyncRef = useRef<{ id?: string; status?: number }>({});
  useEffect(() => {
    const id = latest?.id;
    const st = latest?.status;
    if (!id) return;

    const prev = latestRunSyncRef.current;
    const idChanged = prev.id !== id;
    const statusChanged = prev.status !== st;
    latestRunSyncRef.current = { id, status: st };

    if (idChanged || statusChanged) {
      void queryClient.invalidateQueries({
        queryKey: getGetApiAdminBackupRunsIdQueryKey(id),
      });
    }

    if (statusChanged && st !== undefined) {
      const wasActive = isBackupLatestRunActiveStatus(prev.status);
      const nowTerminal =
        st === BackupRunResponseDtoStatus.NUMBER_3 ||
        st === BackupRunResponseDtoStatus.NUMBER_4 ||
        st === BackupRunResponseDtoStatus.NUMBER_5 ||
        st === BackupRunResponseDtoStatus.NUMBER_6;
      if (nowTerminal && (wasActive || idChanged)) {
        void queryClient.invalidateQueries({
          queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
        });
        void queryClient.invalidateQueries({
          queryKey: getGetApiAdminBackupVerificationLatestQueryKey(),
        });
        void queryClient.invalidateQueries({
          queryKey: ["/api/admin/backup/runs"],
        });
      }
    }
  }, [latest?.id, latest?.status, queryClient]);

  const invalidateAll = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ["/api/admin/backup"] });
    await queryClient.invalidateQueries({
      queryKey: getGetApiAdminBackupExecutionModeQueryKey(),
    });
    await queryClient.invalidateQueries({
      queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
    });
    await queryClient.invalidateQueries({
      queryKey: ["/api/admin/restore-verification"],
    });
  }, [queryClient]);

  const health = statusQuery.data?.configurationHealth;
  const healthLv = normalizeHealthLevelString(health?.level);
  const restoreReady = restoreReadinessQuery.data;
  const restoreLv = normalizeHealthLevelString(restoreReady?.level);

  const detailForPipeline = runDetailQuery.data ?? null;

  const operatorTruth = useMemo(
    () =>
      buildBackupOperatorTruthModel({
        t,
        health,
        healthLv,
        restoreReady,
        restoreLv,
        latest,
        detailForPipeline,
        verification: verificationForTruth,
        restoreLatest: restoreLatestForTruth,
        recoverabilitySummary: recoverabilityQuery.data,
        restoreCapability: statusQuery.data?.restore,
        externalCopyVariant: mapArtifactsToExternalCopyVariant(
          detailForPipeline?.artifacts,
        ),
        restoreNotes: statusQuery.data?.restore?.notes?.trim(),
        omitDedicatedSectionIssueDuplicates: true,
        executionModeDto: executionModeQuery.data ?? null,
        hasStatusPayload: Boolean(statusQuery.data),
        suppressRestoreDrillFailureInHealthBanner: true,
      }),
    [
      t,
      health,
      healthLv,
      restoreReady,
      restoreLv,
      latest,
      detailForPipeline,
      verificationForTruth,
      restoreLatestForTruth,
      recoverabilityQuery.data,
      statusQuery.data?.restore,
      executionModeQuery.data,
      statusQuery.data,
    ],
  );

  const restoreRunExtended = useMemo(
    () => extendRestoreVerificationRunDto(restoreLatestForTruth),
    [restoreLatestForTruth],
  );

  const drProofPresentation = useMemo(
    () =>
      buildDrProofPresentationModel({
        truth: operatorTruth,
        latest,
        detailForPipeline,
        verification: verificationForTruth,
        recoverability: recoverabilityQuery.data,
        restoreLatest: restoreLatestForTruth,
        restoreExtended: restoreRunExtended,
      }),
    [
      operatorTruth,
      latest,
      detailForPipeline,
      verificationForTruth,
      recoverabilityQuery.data,
      restoreLatestForTruth,
      restoreRunExtended,
    ],
  );

  const drProofScanTags = useMemo(
    () =>
      buildDrProofScanTags({
        model: drProofPresentation,
        restoreLatest: restoreLatestForTruth,
        truth: operatorTruth,
      }),
    [drProofPresentation, restoreLatestForTruth, operatorTruth],
  );

  const banner = operatorTruth.banner;
  const alertItems = operatorTruth.alerts;

  const isSimulatedAdapterEnvironment = operatorTruth.simulatedOperationalMode;

  const loading =
    statusQuery.isLoading ||
    runsOrchestrationQuery.isLoading ||
    verificationQuery.isLoading ||
    restoreLatestQuery.isLoading ||
    restoreReadinessQuery.isLoading;

  /** Yalnızca sayfa omurgası; tablo / run-detail hataları ilgili kartta ayrı gösterilir. */
  const error =
    statusQuery.isError ||
    verificationQuery.isError ||
    restoreLatestQuery.isError ||
    restoreReadinessQuery.isError;

  const v = verificationForTruth;
  const rr = restoreLatestForTruth;

  const summaryBackupFootnote = t(
    operatorTruth.summaryPresentation.summaryBackupFootnoteKey,
  );
  const summaryRestoreFootnote = t(
    operatorTruth.summaryPresentation.summaryRestoreFootnoteKey,
  );
  const showRealPgDumpOperationalBanner =
    operatorTruth.summaryPresentation.showRealPgDumpOperationalBanner;
  const showDevRealDumpGuidance =
    operatorTruth.summaryPresentation.showDevRealDumpGuidance;

  /** Simüle / gerçek dump / geçerlilik uyarıları: üst özetle çakışsa da uzun açıklama burada; açık tutulur. */
  const pipelineNoticesDefaultOpen =
    isSimulatedAdapterEnvironment ||
    showRealPgDumpOperationalBanner ||
    Boolean(operatorTruth.operatorValidity);

  /** Üst özette turuncu “simüle” etiketi + şerit varken aynı “stub data plane” geçerlilik Alert’ini tekrarlama. */
  const showPipelineOperatorValidityStrip =
    Boolean(operatorTruth.operatorValidity) &&
    !(
      isSimulatedAdapterEnvironment &&
      operatorTruth.operatorValidity?.severity === "info" &&
      operatorTruth.operatorValidity.titleKey ===
        "backupDr.operatorValidity.stubDataPlaneTitle"
    );

  /** Çekirdek status yüklüyken destekleyici uçlardan biri başarısız — kısmi gerçeklik uyarısı. */
  const partialSupportingQueryFailure =
    Boolean(statusQuery.data) &&
    !statusQuery.isError &&
    (recoverabilityQuery.isError ||
      verificationQuery.isError ||
      restoreLatestQuery.isError);

  return (
    <Space orientation="vertical" size={16} style={{ width: "100%" }}>
      {!embedded ? (
        <div style={{ width: "100%", scrollMarginTop: 72 }}>
          <Typography.Title level={4} style={{ marginTop: 0, marginBottom: 4 }}>
            {t("backupDr.page.title")}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
            {t("backupDr.page.subtitle")}
          </Typography.Paragraph>
        </div>
      ) : null}
      {!hideScheduleSettings ? <BackupScheduleSettings canManage={canManage} /> : null}
      <div style={{ width: "100%", scrollMarginTop: embedded ? 0 : 72 }}>
        <Card size="small">
          <Space orientation="vertical" size="small" style={{ width: "100%" }}>
            <Space wrap align="center">
              <Button
                icon={<ReloadOutlined />}
                onClick={() => invalidateAll()}
                loading={statusQuery.isFetching}
              >
                {t("backupDr.actions.refresh")}
              </Button>
              {!canManage && (
                <Typography.Text type="secondary">
                  {t("backupDr.permission.noManage")}
                </Typography.Text>
              )}
            </Space>
            {error && (
              <Alert
                type="error"
                showIcon
                title={t("backupDr.errors.loadFailed")}
                action={
                  <Button onClick={() => invalidateAll()}>
                    {t("backupDr.actions.refresh")}
                  </Button>
                }
              />
            )}
            <Collapse
              size="small"
              bordered={false}
              items={[
                {
                  key: "scope",
                  label: t("backupDr.scope.collapseLabel"),
                  children: (
                    <Alert
                      type="info"
                      showIcon
                      title={t("backupDr.scope.title")}
                      description={
                        <div>
                          <Typography.Paragraph style={{ marginBottom: 8 }}>
                            {t("backupDr.scope.body")}
                          </Typography.Paragraph>
                          <Typography.Paragraph
                            type="secondary"
                            style={{ marginBottom: 8, marginTop: 0 }}
                          >
                            {t("backupDr.scope.devRealPgLead")}
                          </Typography.Paragraph>
                          <Typography.Paragraph
                            type="secondary"
                            style={{ marginBottom: 0, marginTop: 0, fontSize: 13 }}
                          >
                            <Typography.Link href="#backup-dr-dev-pgdump-checklist">
                              {t("backupDr.devRealPgDump.jumpToChecklist")}
                            </Typography.Link>
                            <Typography.Text type="secondary">
                              {" · "}
                              {t("backupDr.devRealPgDump.docRepoFileLead")}{" "}
                              <Typography.Text
                                code
                                copyable={{ text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH }}
                              >
                                {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                              </Typography.Text>
                            </Typography.Text>
                          </Typography.Paragraph>
                        </div>
                      }
                    />
                  ),
                },
              ]}
            />
          </Space>
        </Card>
      </div>

      {loading && !statusQuery.data ? (
        <Spin />
      ) : (
        <>
          <Space orientation="vertical" size={12} style={{ width: "100%" }}>
            <BackupDrDataFreshnessStrip
              show={partialSupportingQueryFailure}
              recoverabilityFailed={recoverabilityQuery.isError}
              verificationFailed={verificationQuery.isError}
              restoreLatestFailed={restoreLatestQuery.isError}
              onRetry={() => void invalidateAll()}
              t={t}
            />
            <BackupDrPostureSummary
              drProof={drProofPresentation}
              scanTags={drProofScanTags}
              latestRun={latest}
              restoreLatest={restoreLatestForTruth}
              recoverability={recoverabilityQuery.data}
              executionMode={operatorTruth.executionMode}
              simulatedOperationalMode={isSimulatedAdapterEnvironment}
              backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
              restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
              formatDt={formatDt}
              formatLocale={formatLocale}
              t={t}
            />
          </Space>

          <BackupMonitoringSection
            statusPayload={statusQuery.data}
            latest={latest}
            latestDetail={detailForPipeline ?? undefined}
            configurationHealth={health}
            artifactPipelinePolicy={policy}
            canManage={canManage}
            recoverability={recoverabilityQuery.data}
            restoreLatest={restoreLatestForTruth}
            restoreReadiness={restoreReady}
            simulatedOperationalMode={isSimulatedAdapterEnvironment}
            backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
            restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
            formatDt={formatDt}
            formatLocale={formatLocale}
            metricsPollInterval={pollAlignedWithLatestBackup}
            loading={loading}
            t={t}
          />

          <BackupDrSection
            titleKey="backupDr.dashboardSections.proofAndPosture"
            descriptionKey="backupDr.dashboardSections.proofAndPostureDesc"
            t={t}
          >
            <Space orientation="vertical" size={12} style={{ width: "100%" }}>
              <HealthBanner
                critical={banner.critical}
                warn={banner.warn}
                info={banner.info}
                t={t}
                onRefresh={() => invalidateAll()}
              />
              <BackupDrEvidenceSurface
                models={{
                  drProof: drProofPresentation,
                  evidenceLadder: operatorTruth.evidenceLadder,
                }}
                t={t}
              />
              <BackupDrRecentEvidenceGrid
                recoverability={recoverabilityQuery.data}
                restoreLatest={restoreLatestForTruth}
                latestRun={latest}
                backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
                restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
                formatDt={formatDt}
                formatLocale={formatLocale}
                layout="grouped"
                t={t}
              />
            </Space>
          </BackupDrSection>

          <BackupDrSection
            titleKey="backupDr.dashboardSections.environment"
            descriptionKey="backupDr.dashboardSections.environmentDesc"
            t={t}
          >
            <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
              <Divider orientation="left" plain style={{ marginTop: 0 }}>
                {t("backupDr.ia.drilldownGlance")}
              </Divider>
              <Row gutter={[16, 16]}>
                <Col xs={24} sm={12} lg={6}>
                  <Card size="small">
                    <Statistic
                      title={t("backupDr.summary.backupHealth")}
                      value={t(
                        operatorTruth.summaryPresentation
                          .backupHealthSummaryLabelKey,
                      )}
                      styles={{
                        content: healthStatisticValueStyle(
                        operatorTruth.summaryPresentation.backupHealthUiKind,
                      ),
                      }}
                    />
                    <Typography.Paragraph
                      type="secondary"
                      style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}
                    >
                      {summaryBackupFootnote}
                    </Typography.Paragraph>
                  </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Card size="small">
                    <Statistic
                      title={t("backupDr.summary.restoreReadiness")}
                      value={t(
                        operatorTruth.summaryPresentation
                          .restoreReadinessSummaryLabelKey,
                      )}
                      styles={{
                        content: restoreReadinessStatisticValueStyle(
                        operatorTruth.summaryPresentation
                          .restoreReadinessUiKind,
                      ),
                      }}
                    />
                    <Typography.Paragraph
                      type="secondary"
                      style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}
                    >
                      {summaryRestoreFootnote}
                    </Typography.Paragraph>
                  </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Card size="small">
                    <Statistic
                      title={t("backupDr.summary.configurationShort")}
                      value={
                        operatorTruth.summaryPresentation.configShortSummaryLine
                      }
                    />
                    {isSimulatedAdapterEnvironment ? (
                      <Tooltip
                        title={
                          <span style={{ whiteSpace: "pre-wrap" }}>
                            {t("backupDr.summary.fakeAdapterConfigNoteTooltip")}
                          </span>
                        }
                      >
                        <Typography.Paragraph
                          type="secondary"
                          style={{
                            marginTop: 8,
                            marginBottom: 0,
                            fontSize: 12,
                            cursor: "help",
                          }}
                        >
                          {t("backupDr.summary.fakeAdapterConfigNote")}
                        </Typography.Paragraph>
                      </Tooltip>
                    ) : null}
                  </Card>
                </Col>
                <Col xs={24} sm={12} lg={6}>
                  <Card size="small">
                    <Statistic
                      title={t("backupDr.summary.externalArchiveCard")}
                      value={operatorTruth.artifact.externalCopyDisplayText}
                    />
                  </Card>
                </Col>
              </Row>

              <Divider orientation="left" plain>
                {t("backupDr.ia.drilldownExecution")}
              </Divider>
              <BackupExecutionModeCard
                canManage={canManage}
                t={t}
                onModeSaved={() => invalidateAll()}
              />

              <Collapse
                size="small"
                defaultActiveKey={
                  pipelineNoticesDefaultOpen ? ["pipelineNotices"] : []
                }
                items={[
                  {
                    key: "pipelineNotices",
                    label: t("backupDr.layout.pipelineNoticesCollapse"),
                    children: (
                      <Space
                        orientation="vertical"
                        size="middle"
                        style={{ width: "100%" }}
                      >
                        {showRealPgDumpOperationalBanner ? (
                          <Alert
                            type={REAL_DUMP_PATH_BANNER_ALERT_TYPE}
                            showIcon
                            title={t("backupDr.realDumpMode.bannerTitle")}
                            description={
                              <Typography.Paragraph
                                style={{ marginBottom: 0 }}
                              >
                                {t("backupDr.realDumpMode.bannerBody")}
                              </Typography.Paragraph>
                            }
                          />
                        ) : null}

                        {isSimulatedAdapterEnvironment ? (
                          <Alert
                            type="info"
                            showIcon
                            title={t("backupDr.fakeMode.bannerTitle")}
                            description={
                              <div>
                                <Typography.Paragraph
                                  style={{ marginBottom: 8 }}
                                >
                                  {t("backupDr.fakeMode.bannerBody")}
                                </Typography.Paragraph>
                                <Typography.Paragraph
                                  type="secondary"
                                  style={{ marginBottom: 8, fontSize: 13 }}
                                >
                                  {t("backupDr.fakeMode.bannerRealBackupPrereq")}
                                </Typography.Paragraph>
                                <Typography.Paragraph
                                  type="secondary"
                                  style={{ marginBottom: 0, fontSize: 13 }}
                                >
                                  <Typography.Link href="#backup-dr-dev-pgdump-checklist">
                                    {t("backupDr.devRealPgDump.jumpToChecklist")}
                                  </Typography.Link>
                                  <Typography.Text type="secondary">
                                    {" · "}
                                    {t("backupDr.devRealPgDump.docRepoFileLead")}{" "}
                                    <Typography.Text
                                      code
                                      copyable={{
                                        text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH,
                                      }}
                                    >
                                      {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                                    </Typography.Text>
                                  </Typography.Text>
                                </Typography.Paragraph>
                              </div>
                            }
                          />
                        ) : null}

                        {showPipelineOperatorValidityStrip &&
                        operatorTruth.operatorValidity ? (
                          <Alert
                            type={mapOperatorValidityStripToAlertType(
                              operatorTruth.operatorValidity.severity,
                            )}
                            showIcon
                            title={t(
                              operatorTruth.operatorValidity.titleKey,
                            )}
                            description={
                              <Typography.Paragraph
                                style={{ marginBottom: 0 }}
                              >
                                {t(
                                  operatorTruth.operatorValidity.descriptionKey,
                                )}
                              </Typography.Paragraph>
                            }
                          />
                        ) : null}
                      </Space>
                    ),
                  },
                ]}
              />

              <BackupRecoverabilityCard
                formatDt={formatDt}
                formatLocale={formatLocale}
                backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
                restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
                simulatedOperationalMode={isSimulatedAdapterEnvironment}
                omitSimulatedEnvironmentStrip={isSimulatedAdapterEnvironment}
                omitProofGapCaveat
                hideProofTimestampBlock
                t={t}
              />

              <Card
                id="backup-dr-nondb-checklist"
                size="small"
                title={t("backupDr.confidenceDashboard.sections.nonDbProof")}
              >
                <Typography.Paragraph style={{ marginBottom: 8 }}>
                  {t("backupDr.confidenceDashboard.nonDb.intro")}
                </Typography.Paragraph>
                <ul style={{ marginBottom: 0, paddingLeft: 20 }}>
                  <li>{t("backupDr.confidenceDashboard.nonDb.tse")}</li>
                  <li>{t("backupDr.confidenceDashboard.nonDb.secrets")}</li>
                  <li>{t("backupDr.confidenceDashboard.nonDb.tooling")}</li>
                  <li>{t("backupDr.confidenceDashboard.nonDb.archive")}</li>
                  <li>
                    {t("backupDr.confidenceDashboard.nonDb.finanzOnline")}
                  </li>
                </ul>
              </Card>
            </Space>
          </BackupDrSection>

          <BackupDrSection titleKey="backupDr.dashboardSections.liveRun" t={t}>
            <Row gutter={[16, 16]}>
              <Col xs={24} lg={14}>
                <BackupRunProgressBanner
                  latest={latest}
                  isSimulatedExecution={operatorTruth.run.simulatedEvidence}
                  recoverabilityNotProven={
                    operatorTruth.progressRunBanner
                      .recoverabilityNotProvenGlance
                  }
                  latestRestoreDrillFailed={
                    operatorTruth.progressRunBanner.latestRestoreDrillFailed
                  }
                  omitSimulatedSuccessDetail={false}
                  averageSucceededDurationSeconds={
                    statusQuery.data?.averageSucceededBackupDurationSeconds ??
                    undefined
                  }
                  averageSucceededDurationSampleCount={
                    statusQuery.data?.averageSucceededBackupDurationSampleCount
                  }
                  formatDt={formatDt}
                  formatLocale={formatLocale}
                  t={t}
                />
                <BackupStatusCard
                  formatDt={formatDt}
                  formatLocale={formatLocale}
                  backupStatusTagColor={mapBackupRunStatusAntdColor}
                  backupStatusLabel={(s) =>
                    operatorTruth.labels.backupStatus(s)
                  }
                  allowClientPipelineFallback={allowClientPipelineFallback}
                  showLatestRunVsRecoverabilityHint
                  simulatedOperationalMode={isSimulatedAdapterEnvironment}
                  omitFakeOperationalNotice={isSimulatedAdapterEnvironment}
                  operatorRunTruth={{
                    technicalSuccess: operatorTruth.run.technicalSuccess,
                    simulatedEvidence: operatorTruth.run.simulatedEvidence,
                  }}
                  t={t}
                />
                {latest?.id &&
                latest.status === BackupRunResponseDtoStatus.NUMBER_3 ? (
                  <BackupArtifactsDownloadCard
                    variant="latest_success"
                    runId={latest.id}
                    artifacts={detailForPipeline?.artifacts ?? []}
                    canManage={canManage}
                    isSimulatedExecution={operatorTruth.run.simulatedEvidence}
                    runAdapterKind={detailForPipeline?.adapterKind ?? undefined}
                    realPostgreSqlLogicalDumpConfigured={
                      recoverabilityQuery.data
                        ?.realPostgreSqlLogicalDumpConfigured
                    }
                    simulatedOperationalMode={
                      operatorTruth.simulatedOperationalMode
                    }
                    loadingArtifacts={
                      (runDetailQuery.isLoading || runDetailQuery.isFetching) &&
                      Boolean(latest.id)
                    }
                    t={t}
                  />
                ) : null}
                {offerLastKnownGoodDownloads && lkgRunId ? (
                  lkgRunDetailQuery.isError ? (
                    <Alert
                      type="warning"
                      showIcon
                      style={{ marginTop: 16 }}
                      title={t("backupDr.download.lkgLoadFailed")}
                      action={
                        <Button
                          size="small"
                          onClick={() => void lkgRunDetailQuery.refetch()}
                        >
                          {t("backupDr.actions.refresh")}
                        </Button>
                      }
                    />
                  ) : (
                    <BackupArtifactsDownloadCard
                      variant="last_known_good"
                      runId={lkgRunId}
                      artifacts={lkgRunDetailQuery.data?.artifacts ?? []}
                      canManage={canManage}
                      isSimulatedExecution={
                        lkgRunDetailQuery.data?.isSimulatedExecution ??
                        recoverabilityQuery.data
                          ?.lastSuccessfulBackupRunIsSimulatedExecution ??
                        undefined
                      }
                      runAdapterKind={
                        lkgRunDetailQuery.data?.adapterKind ?? undefined
                      }
                      realPostgreSqlLogicalDumpConfigured={
                        recoverabilityQuery.data
                          ?.realPostgreSqlLogicalDumpConfigured
                      }
                      simulatedOperationalMode={
                        isSimulatedBackupAdapterKind(
                          lkgRunDetailQuery.data?.adapterKind,
                        ) ||
                        lkgRunDetailQuery.data?.isSimulatedExecution === true ||
                        recoverabilityQuery.data
                          ?.lastSuccessfulBackupRunIsSimulatedExecution === true
                      }
                      loadingArtifacts={
                        lkgRunDetailQuery.isLoading ||
                        lkgRunDetailQuery.isFetching
                      }
                      t={t}
                    />
                  )
                ) : null}
              </Col>
              <Col xs={24} lg={10}>
                <Card title={t("backupDr.externalCopy.title")} size="small">
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginTop: 0, marginBottom: 12 }}
                  >
                    {t("backupDr.externalCopy.scopeFromLatestRun")}
                  </Typography.Paragraph>
                  {runDetailQuery.isLoading && latest?.id ? (
                    <Spin description={t("backupDr.externalCopy.loading")} />
                  ) : (
                    <Alert
                      type={mapExternalCopyVariantToAlertType(
                        operatorTruth.artifact.externalCopyVariant,
                      )}
                      showIcon
                      title={operatorTruth.artifact.externalCopyDisplayText}
                    />
                  )}
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginTop: 8, marginBottom: 0 }}
                  >
                    {t("backupDr.restoreCapability.title")}:{" "}
                    {t(
                      operatorTruth.restore.backendReportedCapability.labelKey,
                    )}
                  </Typography.Paragraph>
                </Card>
              </Col>
            </Row>
          </BackupDrSection>

          <BackupDrSection
            titleKey="backupDr.dashboardSections.restoreDrill"
            t={t}
          >
            <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
              <Card
                title={t("backupDr.artifactVerification.title")}
                size="small"
              >
                {!v ? (
                  <Typography.Text type="secondary">
                    {t("backupDr.artifactVerification.none")}
                  </Typography.Text>
                ) : (
                  <Descriptions column={1} size="small" bordered>
                    <Descriptions.Item label={t("backupDr.table.status")}>
                      <Tag
                        color={
                          v.status === 1
                            ? "blue"
                            : v.status === 2
                              ? "error"
                              : "processing"
                        }
                      >
                        {t(`backupDr.verificationStatus.${v.status}`)}
                      </Tag>
                    </Descriptions.Item>
                    <Descriptions.Item label={t("backupDr.table.verifier")}>
                      {v.verifierSource}
                    </Descriptions.Item>
                    <Descriptions.Item label={t("backupDr.latestRun.completed")}>
                      {formatDt(v.completedAt, formatLocale)}
                    </Descriptions.Item>
                  </Descriptions>
                )}
                <Typography.Paragraph
                  type="secondary"
                  style={{ marginTop: 12, marginBottom: 0 }}
                >
                  {t("backupDr.artifactVerification.notRestoreProof")}
                </Typography.Paragraph>
              </Card>

              <Row gutter={[16, 16]}>
              <Col xs={24} lg={12}>
                <RestoreVerificationCard
                  run={rr}
                  formatDt={formatDt}
                  formatLocale={formatLocale}
                  restoreStatusTagColor={mapRestoreVerificationStatusAntdColor}
                  restoreStatusLabel={(s) =>
                    operatorTruth.labels.restoreStatus(s)
                  }
                  dumpInspectionTriState={mapDumpInspectionTriState}
                  isSimulatedBackupPipeline={isSimulatedAdapterEnvironment}
                  backupWorkerRealProfileBlocked={
                    operatorTruth.executionMode.loaded &&
                    operatorTruth.executionMode.effectiveIsPgDumpAdapter &&
                    !operatorTruth.executionMode.effectiveModeRunnable
                  }
                  t={t}
                />
              </Col>
              <Col xs={24} lg={12}>
                <Card title={t("backupDr.readiness.title")} size="small">
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginTop: 0, marginBottom: 12, fontSize: 13 }}
                  >
                    {t("backupDr.readiness.notEndToEndDr")}
                  </Typography.Paragraph>
                  {!restoreReady ? (
                    <Typography.Text type="secondary">
                      {t("backupDr.summary.unknown")}
                    </Typography.Text>
                  ) : (
                    <>
                      {normalizeHealthLevelString(restoreReady.level) !==
                      normalizeHealthLevelString(
                        operatorTruth.restore.effectiveReadinessLevel ?? "",
                      ) ? (
                        <Typography.Paragraph
                          type="warning"
                          style={{ marginBottom: 12 }}
                        >
                          {t("backupDr.readiness.levelCappedForOperatorTruth")}
                        </Typography.Paragraph>
                      ) : null}
                      <Typography.Paragraph
                        type="secondary"
                        style={{ marginBottom: 8, fontSize: 12 }}
                      >
                        {t("backupDr.readiness.backendReportedSignal")}
                      </Typography.Paragraph>
                      <Space wrap style={{ marginBottom: 12 }}>
                        <Tag
                          color={
                            operatorTruth.summaryPresentation
                              .restoreReadinessUiKind === "unhealthy"
                              ? "red"
                              : operatorTruth.summaryPresentation
                                    .restoreReadinessUiKind === "degraded"
                                ? "orange"
                                : operatorTruth.summaryPresentation
                                      .restoreReadinessUiKind === "healthy"
                                  ? "blue"
                                  : "default"
                          }
                        >
                          {t(
                            operatorTruth.summaryPresentation
                              .restoreReadinessSummaryLabelKey,
                          )}
                        </Tag>
                        <Tag
                          color={restoreReady.workerEnabled ? "blue" : "orange"}
                        >
                          {t("backupDr.readiness.restoreWorker")}:{" "}
                          {restoreReady.workerEnabled
                            ? t("common.buttons.yes")
                            : t("common.buttons.no")}
                        </Tag>
                        <Tag
                          color={
                            restoreReady.orchestratorDistributedLockEnabled
                              ? "blue"
                              : "orange"
                          }
                        >
                          {t("backupDr.lock.restoreDistributedLock")}:{" "}
                          {restoreReady.orchestratorDistributedLockEnabled
                            ? t("common.buttons.yes")
                            : t("common.buttons.no")}
                        </Tag>
                      </Space>
                      {restoreReady.scopeDisclaimer ? (
                        <Typography.Paragraph type="secondary">
                          {restoreReady.scopeDisclaimer}
                        </Typography.Paragraph>
                      ) : null}
                      {restoreReady.issues?.length ? (
                        <ul style={{ marginBottom: 0 }}>
                          {restoreReady.issues.map((issue, i) => (
                            <li key={i}>
                              <Typography.Text>{issue}</Typography.Text>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <Typography.Text type="secondary">
                          {t("backupDr.readiness.noIssues")}
                        </Typography.Text>
                      )}
                    </>
                  )}
                </Card>
              </Col>
              </Row>
            </Space>
          </BackupDrSection>

          <BackupDrSection
            titleKey="backupDr.dashboardSections.configuration"
            t={t}
          >
            <Collapse
              size="small"
              defaultActiveKey={["configuration"]}
              items={[
                {
                  key: "configuration",
                  label: t("backupDr.layout.configurationCollapse"),
                  children: (
            <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
              <Row gutter={[16, 16]}>
                <Col xs={24} lg={12}>
                  <Card title={t("backupDr.pipeline.title")} size="small">
                    {!policy ? (
                      <Typography.Text type="secondary">
                        {t("backupDr.pipeline.none")}
                      </Typography.Text>
                    ) : (
                      <Descriptions column={1} size="small" bordered>
                        <Descriptions.Item
                          label={t("backupDr.pipeline.externalRequirement")}
                        >
                          {policy.externalArchiveRequirement ?? "—"}
                        </Descriptions.Item>
                        <Descriptions.Item
                          label={t("backupDr.pipeline.stagingConfigured")}
                        >
                          {policy.artifactStagingRootConfigured === undefined
                            ? "—"
                            : policy.artifactStagingRootConfigured
                              ? t("common.buttons.yes")
                              : t("common.buttons.no")}
                        </Descriptions.Item>
                        <Descriptions.Item
                          label={t("backupDr.pipeline.externalConfigured")}
                        >
                          {policy.externalArchiveRootConfigured === undefined
                            ? "—"
                            : policy.externalArchiveRootConfigured
                              ? t("common.buttons.yes")
                              : t("common.buttons.no")}
                        </Descriptions.Item>
                        <Descriptions.Item
                          label={t("backupDr.pipeline.runExternalWhenEligible")}
                        >
                          {policy.willRunExternalArchiveAfterStagingVerificationWhenEligible ===
                          undefined
                            ? "—"
                            : policy.willRunExternalArchiveAfterStagingVerificationWhenEligible
                              ? t("common.buttons.yes")
                              : t("common.buttons.no")}
                        </Descriptions.Item>
                        <Descriptions.Item
                          label={t("backupDr.pipeline.hashReverify")}
                        >
                          {policy.stagingOnDiskHashReverificationExpected ===
                          undefined
                            ? "—"
                            : policy.stagingOnDiskHashReverificationExpected
                              ? t("common.buttons.yes")
                              : t("common.buttons.no")}
                        </Descriptions.Item>
                        <Descriptions.Item label={t("backupDr.health.adapter")}>
                          {policy.effectiveAdapterKind ?? "—"}
                        </Descriptions.Item>
                      </Descriptions>
                    )}
                    {policy?.operatorNotes?.length ? (
                      <Typography.Paragraph style={{ marginTop: 12 }}>
                        <strong>{t("backupDr.pipeline.operatorNotes")}</strong>
                        <ul>
                          {policy.operatorNotes.map((n, i) => (
                            <li key={i}>{n}</li>
                          ))}
                        </ul>
                      </Typography.Paragraph>
                    ) : null}
                  </Card>
                </Col>
                <Col xs={24} lg={12}>
                  <Card title={t("backupDr.health.title")} size="small">
                    <Space
                      orientation="vertical"
                      style={{ width: "100%" }}
                      size="middle"
                    >
                      <div>
                        <Tag
                          color={tagColorForConfigurationHealthUiKind(
                            operatorTruth.summaryPresentation
                              .backupHealthUiKind,
                          )}
                        >
                          {t(
                            operatorTruth.summaryPresentation
                              .backupHealthSummaryLabelKey,
                          )}
                        </Tag>
                        <Typography.Text type="secondary">
                          {" "}
                          {t("backupDr.health.adapter")}:{" "}
                          {health?.effectiveAdapterKind ?? "—"} ·{" "}
                          {t("backupDr.health.worker")}:{" "}
                          {health?.workerEnabled
                            ? t("common.buttons.yes")
                            : t("common.buttons.no")}
                        </Typography.Text>
                      </div>
                      {health?.diagnostics && health.diagnostics.length > 0 ? (
                        <div>
                          <Typography.Text
                            type="secondary"
                            style={{ fontSize: 12 }}
                          >
                            {t("backupDr.health.diagnosticsIntro")}
                          </Typography.Text>
                          <div style={{ marginTop: 8 }}>
                            <Space wrap size={[4, 8]}>
                              {health.diagnostics.slice(0, 10).map((d, i) => (
                                <Tooltip
                                  key={`${d.code}-${i}`}
                                  title={d.message}
                                >
                                  <Tag
                                    color={backupDiagnosticTagColor(d.severity)}
                                  >
                                    {d.code}
                                  </Tag>
                                </Tooltip>
                              ))}
                            </Space>
                            {health.diagnostics.length > 10 ? (
                              <Typography.Paragraph
                                type="secondary"
                                style={{
                                  marginTop: 8,
                                  marginBottom: 0,
                                  fontSize: 12,
                                }}
                              >
                                {t("backupDr.health.diagnosticsTruncated", {
                                  count: String(health.diagnostics.length - 10),
                                })}
                              </Typography.Paragraph>
                            ) : null}
                          </div>
                        </div>
                      ) : null}
                      {health?.issues?.length ? (
                        <ul style={{ marginBottom: 0 }}>
                          {health.issues.map((issue, i) => (
                            <li key={i}>
                              <Typography.Text>{issue}</Typography.Text>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <Typography.Text type="secondary">
                          {t("backupDr.readiness.noIssues")}
                        </Typography.Text>
                      )}
                    </Space>
                  </Card>
                </Col>
              </Row>

              <Card title={t("backupDr.lock.title")} size="small">
                <Row gutter={[16, 16]}>
                  <Col xs={24} md={12}>
                    <Typography.Title level={5}>
                      {t("backupDr.lock.restoreSide")}
                    </Typography.Title>
                    <Descriptions column={1} size="small" bordered>
                      <Descriptions.Item
                        label={t("backupDr.lock.restoreDistributedLock")}
                      >
                        {restoreReady?.orchestratorDistributedLockEnabled ===
                        undefined
                          ? "—"
                          : restoreReady.orchestratorDistributedLockEnabled
                            ? t("common.buttons.yes")
                            : t("common.buttons.no")}
                      </Descriptions.Item>
                      <Descriptions.Item
                        label={t("backupDr.readiness.restoreWorker")}
                      >
                        {restoreReady?.workerEnabled === undefined
                          ? "—"
                          : restoreReady.workerEnabled
                            ? t("common.buttons.yes")
                            : t("common.buttons.no")}
                      </Descriptions.Item>
                    </Descriptions>
                    {operatorTruth.lockHints.restore.length ? (
                      <Alert
                        style={{ marginTop: 12 }}
                        type="warning"
                        showIcon
                        title={t("backupDr.lock.hintsFromApi")}
                        description={
                          <ul style={{ marginBottom: 0 }}>
                            {operatorTruth.lockHints.restore.map((x, i) => (
                              <li key={i}>{x}</li>
                            ))}
                          </ul>
                        }
                      />
                    ) : null}
                  </Col>
                  <Col xs={24} md={12}>
                    <Typography.Title level={5}>
                      {t("backupDr.lock.backupSide")}
                    </Typography.Title>
                    <Typography.Paragraph type="secondary">
                      {t("backupDr.lock.backupSideHint")}
                    </Typography.Paragraph>
                    {operatorTruth.lockHints.backup.length ? (
                      <Alert
                        type="warning"
                        showIcon
                        title={t("backupDr.lock.hintsFromApi")}
                        description={
                          <ul style={{ marginBottom: 0 }}>
                            {operatorTruth.lockHints.backup.map((x, i) => (
                              <li key={i}>{x}</li>
                            ))}
                          </ul>
                        }
                      />
                    ) : (
                      <Typography.Text type="secondary">
                        {t("backupDr.lock.noLockHints")}
                      </Typography.Text>
                    )}
                  </Col>
                </Row>
              </Card>
            </Space>
                  ),
                },
              ]}
            />
          </BackupDrSection>

          <BackupDrSection
            titleKey="backupDr.dashboardSections.reference"
            t={t}
          >
            <Space orientation="vertical" size="middle" style={{ width: "100%" }}>
              <Alert
                type="info"
                showIcon
                title={t("backupDr.knownGaps.title")}
                description={t("backupDr.knownGaps.body")}
              />

              {showDevRealDumpGuidance ? (
                <Card
                  id="backup-dr-dev-pgdump-checklist"
                  size="small"
                  title={t("backupDr.devRealPgDump.title")}
                >
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginTop: 0 }}
                  >
                    {t("backupDr.devRealPgDump.intro")}
                  </Typography.Paragraph>
                  <Typography.Title
                    level={5}
                    style={{ marginTop: 8, marginBottom: 8 }}
                  >
                    {t("backupDr.devRealPgDump.sectionEnableTitle")}
                  </Typography.Title>
                  <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.itemAdapter")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.itemStaging")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.itemConnection")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.itemBinaries")}
                      </Typography.Text>
                    </li>
                  </ul>
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 8, fontSize: 12 }}
                  >
                    {t("backupDr.devRealPgDump.expectedOutputPath")}
                  </Typography.Paragraph>
                  <Typography.Title
                    level={5}
                    style={{ marginTop: 8, marginBottom: 8 }}
                  >
                    {t("backupDr.devRealPgDump.sectionTroubleshootingTitle")}
                  </Typography.Title>
                  <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.stuckFakeAdapter")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.stuckStaging")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.stuckConnection")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.stuckPgDump")}
                      </Typography.Text>
                    </li>
                    <li>
                      <Typography.Text>
                        {t("backupDr.devRealPgDump.stuckPgRestore")}
                      </Typography.Text>
                    </li>
                  </ul>
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 8, fontSize: 12 }}
                  >
                    {t("backupDr.devRealPgDump.startupLogsHint")}
                  </Typography.Paragraph>
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 8, fontSize: 12 }}
                  >
                    {t("backupDr.devRealPgDump.docHint")}
                  </Typography.Paragraph>
                  <Typography.Paragraph
                    type="secondary"
                    style={{ marginBottom: 0, fontSize: 12 }}
                  >
                    {t("backupDr.devRealPgDump.docRepoFileLead")}{" "}
                    <Typography.Text
                      code
                      copyable={{ text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH }}
                    >
                      {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                    </Typography.Text>
                  </Typography.Paragraph>
                </Card>
              ) : null}

              <Card title={t("backupDr.distinction.title")} size="small">
                <Typography.Paragraph>
                  {t("backupDr.distinction.body")}
                </Typography.Paragraph>
              </Card>

              <Card title={t("backupDr.disclaimer.title")} size="small">
                <Typography.Paragraph>
                  {health?.artifactVerificationDisclaimer ?? "—"}
                </Typography.Paragraph>
              </Card>

              <Card title={t("backupDr.alerts.title")} size="small">
                {alertItems.length === 0 ? (
                  <Typography.Text type="secondary">
                    {t("backupDr.alerts.none")}
                  </Typography.Text>
                ) : (
                  <Space
                    orientation="vertical"
                    style={{ width: "100%" }}
                    size="small"
                  >
                    {alertItems.map((item, i) => (
                      <Alert
                        key={i}
                        type={
                          item.severity === "error"
                            ? "error"
                            : item.severity === "warning"
                              ? "warning"
                              : "info"
                        }
                        showIcon
                        title={item.text}
                      />
                    ))}
                  </Space>
                )}
              </Card>
            </Space>
          </BackupDrSection>

          <BackupDrSection
            titleKey="backupDr.dashboardSections.manualActions"
            t={t}
          >
            <BackupManualActionsPanel
              canManage={canManage}
              simulatedOperationalMode={isSimulatedAdapterEnvironment}
              modeAwareConfirmations={
                operatorTruth.manualActionsModeConfirmations
              }
              t={t}
            />
          </BackupDrSection>

          <BackupDrSection titleKey="backupDr.dashboardSections.history" t={t}>
            <Space orientation="vertical" size="large" style={{ width: "100%" }}>
              <BackupRecentRunsTable
                backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
                formatDt={formatDt}
                formatLocale={formatLocale}
                t={t}
                onRetryInvalidate={invalidateAll}
                canRequestManualRestore={canRequestManualRestore}
                onRequestManualRestore={setManualRestoreRun}
                onSelectRun={
                  onSelectBackupRun
                    ? (run) => {
                        if (run.id) onSelectBackupRun(run);
                      }
                    : undefined
                }
              />

              {manualRestoreRun ? (
                <RestoreRequestModal
                  open
                  backupRun={manualRestoreRun}
                  onClose={() => setManualRestoreRun(null)}
                  t={t}
                />
              ) : null}

              <ManualRestoreRequestsTable
                canApprove={canRequestManualRestore}
                formatDt={formatDt}
                formatLocale={formatLocale}
                onApprove={setManualRestoreApprovalId}
                t={t}
              />

              {manualRestoreApprovalId ? (
                <RestoreApprovalModal
                  open
                  requestId={manualRestoreApprovalId}
                  onApproved={() => {
                    setManualRestoreApprovalId(null);
                    void invalidateAll();
                  }}
                  onClose={() => setManualRestoreApprovalId(null)}
                  t={t}
                />
              ) : null}

              <BackupRecentRestoreDrillsTable
                formatDt={formatDt}
                formatLocale={formatLocale}
                restoreStatusLabel={(s) =>
                  operatorTruth.labels.restoreStatus(s)
                }
                isSimulatedAdapterEnvironment={isSimulatedAdapterEnvironment}
                t={t}
                onRetryInvalidate={invalidateAll}
              />
            </Space>
          </BackupDrSection>
        </>
      )}
    </Space>
  );
}
