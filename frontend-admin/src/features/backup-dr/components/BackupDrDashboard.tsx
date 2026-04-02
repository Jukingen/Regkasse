'use client';

/**
 * Backup & DR panosu: salt görünürlük + yalnızca API kuyruğa alma; pipeline adımları DTO’dan türetilir.
 */

import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import axios from 'axios';
import { Alert, Button, Card, Col, Descriptions, Row, Space, Spin, Statistic, Tag, Tooltip, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useI18n } from '@/i18n';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  getGetApiAdminBackupRecoverabilitySummaryQueryKey,
  getGetApiAdminBackupRunsIdQueryKey,
  getGetApiAdminBackupRunsQueryKey,
  getGetApiAdminBackupVerificationLatestQueryKey,
  useGetApiAdminBackupRecoverabilitySummary,
  useGetApiAdminBackupRuns,
  useGetApiAdminBackupStatusLatest,
  useGetApiAdminBackupVerificationLatest,
  useGetApiAdminBackupRunsId,
  usePostApiAdminBackupTrigger,
} from '@/api/generated/admin-backup/admin-backup';
import {
  getGetApiAdminRestoreVerificationReadinessQueryKey,
  getGetApiAdminRestoreVerificationRunsLatestQueryKey,
  getGetApiAdminRestoreVerificationRunsQueryKey,
  useGetApiAdminRestoreVerificationReadiness,
  useGetApiAdminRestoreVerificationRuns,
  useGetApiAdminRestoreVerificationRunsLatest,
  usePostApiAdminRestoreVerificationTrigger,
} from '@/api/generated/admin-restore-verification/admin-restore-verification';
import {
  BackupRunResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
  RestoreVerificationTriggerOrchestrationState,
  type BackupRunResponseDto,
  type RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  healthStatisticValueStyle,
  restoreReadinessStatisticValueStyle,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapDumpInspectionTriState,
  mapRestoreVerificationStatusAntdColor,
  normalizeHealthLevelString,
  isSimulatedBackupAdapterKind,
} from '@/features/backup-dr/logic/backupDrMappers';
import {
  REAL_DUMP_PATH_BANNER_ALERT_TYPE,
  mapExternalCopyVariantToAlertType,
  mapOperatorValidityStripToAlertType,
} from '@/features/backup-dr/logic/backupDrGlancePresentation';
import { buildBackupOperatorTruthModel, tagColorForConfigurationHealthUiKind } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import { describeBackupTriggerOutcome } from '@/features/backup-dr/logic/backupTriggerOutcome';
import { BackupArtifactsDownloadCard } from '@/features/backup-dr/components/BackupArtifactsDownloadCard';
import { BackupRunProgressBanner } from '@/features/backup-dr/components/BackupRunProgressBanner';
import { BackupStatusCard } from '@/features/backup-dr/components/BackupStatusCard';
import { HealthBanner } from '@/features/backup-dr/components/HealthBanner';
import { ManualActionsPanel, type ManualActionsPanelProps } from '@/features/backup-dr/components/ManualActionsPanel';
import { RecentRestoreDrillsTable } from '@/features/backup-dr/components/RecentRestoreDrillsTable';
import { RecentRunsTable } from '@/features/backup-dr/components/RecentRunsTable';
import { RestoreVerificationCard } from '@/features/backup-dr/components/RestoreVerificationCard';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';
import { BackupEvidenceLadderCard } from '@/features/backup-dr/components/BackupEvidenceLadderCard';
import { BackupExecutionModeCard } from '@/features/backup-dr/components/BackupExecutionModeCard';
import {
  getBackupExecutionMode,
  getGetApiAdminBackupExecutionModeQueryKey,
} from '@/features/backup-dr/logic/backupExecutionModeApi';
import { isBackupPipelineClientFallbackEnabled } from '@/features/backup-dr/logic/backupPipelineEnv';
import { apiNullableToUndefined } from '@/features/backup-dr/logic/backupDrDtoNormalize';
import { shouldOfferLastKnownGoodArtifactDownload } from '@/features/backup-dr/logic/backupArtifactDownloadTruth';
import {
  BACKUP_ACTIVE_POLL_MS,
  RUN_DETAIL_CATCH_UP_POLL_MS,
  computeRunDetailRefetchIntervalMs,
  isBackupLatestRunActiveStatus,
} from '@/features/backup-dr/logic/backupRunDetailPollPolicy';
import {
  PG_RESTORE_LIST_FAILED,
  interpretPgRestoreListFailure,
  pgRestoreListFailureKindToStatusLabelKey,
  pgRestoreListFailureKindToTagColor,
} from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';

function formatDt(iso: string | undefined | null, formatLocale: string): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString(formatLocale);
  } catch {
    return iso;
  }
}

function triggerErrorMessage(err: unknown, t: (k: string) => string): string {
  if (axios.isAxiosError(err)) {
    const s = err.response?.status;
    if (s === 403) return t('backupDr.errors.forbiddenTrigger');
    if (s === 401) return t('backupDr.errors.unauthorizedTrigger');
    if (s === 409) return t('backupDr.errors.conflictTrigger');
    if (s === 422) return t('backupDr.errors.validationTrigger');
    if (s !== undefined && s >= 500) return t('backupDr.errors.serverTrigger');
  }
  return t('backupDr.errors.triggerFailed');
}

const BACKUP_IDLE_POLL_MS = 60_000;

/** Depo kökünde geliştirici yedek kılavuzu — UI’da gösterilir (Orval DTO ile aynı kaynak fikri). */
const BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH = 'backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md';

function backupDiagnosticTagColor(severity: string | undefined): string {
  const s = (severity ?? '').toLowerCase();
  if (s === 'error') return 'red';
  if (s === 'warning') return 'orange';
  return 'blue';
}

export function BackupDrDashboard() {
  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const allowClientPipelineFallback = isBackupPipelineClientFallbackEnabled();

  const executionModeQuery = useQuery({
    queryKey: getGetApiAdminBackupExecutionModeQueryKey(),
    queryFn: getBackupExecutionMode,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
  });

  const runsParams = useMemo(() => ({ page: 1, pageSize: 15 }), []);
  const restoreParams = useMemo(() => ({ page: 1, pageSize: 10 }), []);

  const pollBackup = useCallback((q: { state: { data?: unknown } }) => {
    const data = q.state.data as { latestRun?: { status?: number } } | undefined;
    const s = data?.latestRun?.status;
    if (isBackupLatestRunActiveStatus(s)) return BACKUP_ACTIVE_POLL_MS;
    return BACKUP_IDLE_POLL_MS;
  }, []);

  const pollRestore = useCallback((q: { state: { data?: unknown } }) => {
    const row = q.state.data as { status?: number } | null | undefined;
    const s = row?.status;
    if (s === 0 || s === 1) return 8_000;
    return 15_000;
  }, []);

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: pollBackup, refetchOnWindowFocus: true },
  });

  const latest = apiNullableToUndefined(statusQuery.data?.latestRun);

  /** Latest-run ile aynı hızda: geçmiş / doğrulama / recoverability, yedek aktifken hizalanır. */
  const pollAlignedWithLatestBackup = useCallback(
    (_query: unknown) => {
      return isBackupLatestRunActiveStatus(latest?.status) ? BACKUP_ACTIVE_POLL_MS : BACKUP_IDLE_POLL_MS;
    },
    [latest?.status],
  );

  /**
   * Run-by-id: aktifken status/latest ile aynı aralıkta yenilenir.
   * Terminalde detail API’si bazen geride kalır — status ile eşleşene kadar kısa aralıkla yakalar, sonra durur.
   */
  const pollRunDetail = useCallback(
    (query: { state: { data?: BackupRunResponseDto | undefined } }) => {
      return computeRunDetailRefetchIntervalMs({
        latestRunId: latest?.id,
        latestStatus: latest?.status,
        detail: query.state.data,
      });
    },
    [latest?.id, latest?.status],
  );

  const runsQuery = useGetApiAdminBackupRuns(runsParams, {
    query: { refetchInterval: pollAlignedWithLatestBackup, refetchOnWindowFocus: true },
  });
  const verificationQuery = useGetApiAdminBackupVerificationLatest({
    query: { refetchInterval: pollAlignedWithLatestBackup, refetchOnWindowFocus: true },
  });
  const recoverabilityQuery = useGetApiAdminBackupRecoverabilitySummary({
    query: { refetchInterval: pollAlignedWithLatestBackup, refetchOnWindowFocus: true },
  });
  const restoreLatestQuery = useGetApiAdminRestoreVerificationRunsLatest({
    query: { refetchInterval: pollRestore, refetchOnWindowFocus: true },
  });
  const restoreHistoryQuery = useGetApiAdminRestoreVerificationRuns(restoreParams, {
    query: { refetchInterval: pollRestore, refetchOnWindowFocus: true },
  });
  const restoreReadinessQuery = useGetApiAdminRestoreVerificationReadiness({
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });

  const verificationForTruth = apiNullableToUndefined(verificationQuery.data);
  const restoreLatestForTruth = apiNullableToUndefined(restoreLatestQuery.data);

  const policy = statusQuery.data?.artifactPipelinePolicy;
  const runDetailQuery = useGetApiAdminBackupRunsId(latest?.id ?? '', {
    query: {
      enabled: Boolean(latest?.id),
      refetchInterval: pollRunDetail,
      refetchOnWindowFocus: true,
    },
  });

  const lkgRunId = recoverabilityQuery.data?.lastSuccessfulBackupRunId?.trim() ?? '';
  const offerLastKnownGoodDownloads = useMemo(
    () =>
      shouldOfferLastKnownGoodArtifactDownload({
        latestRunId: latest?.id,
        latestStatus: latest?.status,
        lastSuccessfulBackupRunId: recoverabilityQuery.data?.lastSuccessfulBackupRunId,
      }),
    [latest?.id, latest?.status, recoverabilityQuery.data?.lastSuccessfulBackupRunId],
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
      void queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRunsIdQueryKey(id) });
    }

    if (statusChanged && st !== undefined) {
      const wasActive = isBackupLatestRunActiveStatus(prev.status);
      const nowTerminal =
        st === BackupRunResponseDtoStatus.NUMBER_3 ||
        st === BackupRunResponseDtoStatus.NUMBER_4 ||
        st === BackupRunResponseDtoStatus.NUMBER_5 ||
        st === BackupRunResponseDtoStatus.NUMBER_6;
      if (nowTerminal && (wasActive || idChanged)) {
        void queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey() });
        void queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupVerificationLatestQueryKey() });
        void queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRunsQueryKey(runsParams) });
      }
    }
  }, [latest?.id, latest?.status, queryClient, runsParams]);

  const invalidateReadiness = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: getGetApiAdminRestoreVerificationReadinessQueryKey() });
  }, [queryClient]);

  const backupTrigger = usePostApiAdminBackupTrigger({
    mutation: {
      onSuccess: async (res) => {
        const fb = describeBackupTriggerOutcome(res);
        const suffix = res.orchestrationState?.trim()
          ? ` ${t('backupDr.messages.orchestrationStateSuffix', { state: res.orchestrationState })}`
          : '';
        const text = `${t(fb.messageKey)}${suffix}`;
        if (fb.level === 'success') message.success(text);
        else message.info(text);
        await queryClient.invalidateQueries({ queryKey: ['/api/admin/backup'] });
        await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey() });
        await invalidateReadiness();
      },
      onError: (err) => message.error(triggerErrorMessage(err, t)),
    },
  });

  const restoreTrigger = usePostApiAdminRestoreVerificationTrigger({
    mutation: {
      onSuccess: async (res) => {
        if (res.newQueuedRunCreated) {
          message.success(t('backupDr.messages.restoreDrillEnqueued'));
        } else if (res.existingRunReturned) {
          if (res.orchestrationState === RestoreVerificationTriggerOrchestrationState.NUMBER_1) {
            message.info(t('backupDr.messages.restoreDrillIdempotent'));
          } else {
            message.info(t('backupDr.messages.restoreDrillExistingActive'));
          }
        }
        await restoreLatestQuery.refetch();
        await restoreHistoryQuery.refetch();
        await queryClient.invalidateQueries({ queryKey: getGetApiAdminRestoreVerificationRunsLatestQueryKey() });
        await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey() });
        await queryClient.invalidateQueries({ queryKey: getGetApiAdminRestoreVerificationRunsQueryKey(restoreParams) });
        await invalidateReadiness();
      },
      onError: (err) => message.error(triggerErrorMessage(err, t)),
    },
  });

  const invalidateAll = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['/api/admin/backup'] });
    await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupExecutionModeQueryKey() });
    await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey() });
    await queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
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
        externalCopyVariant: mapArtifactsToExternalCopyVariant(detailForPipeline?.artifacts),
        restoreNotes: statusQuery.data?.restore?.notes?.trim(),
        omitDedicatedSectionIssueDuplicates: true,
        executionModeDto: executionModeQuery.data ?? null,
        hasStatusPayload: Boolean(statusQuery.data),
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

  const banner = operatorTruth.banner;
  const alertItems = operatorTruth.alerts;

  const isSimulatedAdapterEnvironment = operatorTruth.simulatedOperationalMode;

  const columns: ColumnsType<BackupRunResponseDto> = useMemo(
    () => [
      {
        title: t('backupDr.table.requestedAt'),
        dataIndex: 'requestedAt',
        key: 'requestedAt',
        render: (v: string) => formatDt(v, formatLocale),
      },
      {
        title: t('backupDr.table.status'),
        dataIndex: 'status',
        key: 'status',
        render: (s: number | undefined) => (
          <Tag color={mapBackupRunStatusAntdColor(s)}>{operatorTruth.labels.backupStatus(s)}</Tag>
        ),
      },
      {
        title: t('backupDr.table.adapter'),
        dataIndex: 'adapterKind',
        key: 'adapterKind',
      },
      {
        title: t('backupDr.table.completedAt'),
        dataIndex: 'completedAt',
        key: 'completedAt',
        render: (v: string | null) => formatDt(v, formatLocale),
      },
      {
        title: t('backupDr.table.failure'),
        dataIndex: 'failureCode',
        key: 'failureCode',
        render: (c: string | null) => c ?? '—',
      },
    ],
    [formatLocale, operatorTruth.labels, t],
  );

  const restoreHistoryColumns: ColumnsType<RestoreVerificationRunResponseDto> = useMemo(
    () => [
      {
        title: t('backupDr.latestRun.requested'),
        dataIndex: 'requestedAt',
        key: 'requestedAt',
        render: (x: string) => formatDt(x, formatLocale),
      },
      {
        title: t('backupDr.table.status'),
        dataIndex: 'status',
        key: 'status',
        render: (s: number | undefined, row: RestoreVerificationRunResponseDto) => {
          const listInterp =
            s === RestoreVerificationRunResponseDtoStatus.NUMBER_3 && row.failureCode === PG_RESTORE_LIST_FAILED
              ? interpretPgRestoreListFailure({
                  run: row,
                  isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
                })
              : null;
          const color =
            listInterp != null ? pgRestoreListFailureKindToTagColor(listInterp.kind) : mapRestoreVerificationStatusAntdColor(s);
          const label =
            listInterp != null ? t(pgRestoreListFailureKindToStatusLabelKey(listInterp.kind)) : operatorTruth.labels.restoreStatus(s);
          return <Tag color={color}>{label}</Tag>;
        },
      },
      {
        title: t('backupDr.table.dumpInspection'),
        key: 'dump',
        render: (_: unknown, row: RestoreVerificationRunResponseDto) => {
          const p = mapDumpInspectionTriState(row);
          if (p === undefined) return '—';
          if (p) return t('backupDr.triState.ok');
          const listInterp =
            row.failureCode === PG_RESTORE_LIST_FAILED
              ? interpretPgRestoreListFailure({
                  run: row,
                  isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
                })
              : null;
          if (listInterp?.kind === 'fake_stub_expected') return t('backupDr.triState.dumpInspectionNotApplicableStub');
          return t('backupDr.triState.fail');
        },
      },
      {
        title: t('backupDr.table.restoreAttempt'),
        key: 'attempt',
        render: (_: unknown, row) => {
          if (!row.restoreAttemptExecuted) return t('backupDr.restoreAttempt.notRun');
          if (row.restoreAttemptPassed === true) return t('backupDr.triState.ok');
          if (row.restoreAttemptPassed === false) return t('backupDr.triState.fail');
          return '—';
        },
      },
      {
        title: t('backupDr.table.failure'),
        dataIndex: 'failureCode',
        key: 'failureCode',
        render: (c: string | null | undefined, row: RestoreVerificationRunResponseDto) => {
          const code = c ?? '—';
          if (row.failureCode === PG_RESTORE_LIST_FAILED) {
            const listInterp = interpretPgRestoreListFailure({
              run: row,
              isSimulatedPipelineHeuristic: isSimulatedAdapterEnvironment,
            });
            if (listInterp?.kind === 'fake_stub_expected') {
              return (
                <Tooltip title={t('backupDr.restoreVerification.fakePipeline.pgRestoreListTooltip')}>
                  <span>{code}</span>
                </Tooltip>
              );
            }
            if (listInterp) {
              const tk = `backupDr.restoreVerification.realPipeline.failureTooltips.${listInterp.kind}`;
              const title = t(tk);
              return title !== tk ? (
                <Tooltip title={title}>
                  <span>{code}</span>
                </Tooltip>
              ) : (
                code
              );
            }
          }
          return code;
        },
      },
    ],
    [formatLocale, isSimulatedAdapterEnvironment, operatorTruth.labels, t],
  );

  const loading =
    statusQuery.isLoading ||
    runsQuery.isLoading ||
    verificationQuery.isLoading ||
    restoreLatestQuery.isLoading ||
    restoreReadinessQuery.isLoading;

  /** Yalnızca sayfa omurgası; tablo / run-detail hataları ilgili kartta ayrı gösterilir. */
  const error =
    statusQuery.isError || verificationQuery.isError || restoreLatestQuery.isError || restoreReadinessQuery.isError;

  const v = verificationForTruth;
  const rr = restoreLatestForTruth;

  const summaryBackupFootnote = t(operatorTruth.summaryPresentation.summaryBackupFootnoteKey);
  const summaryRestoreFootnote = t(operatorTruth.summaryPresentation.summaryRestoreFootnoteKey);
  const showRealPgDumpOperationalBanner = operatorTruth.summaryPresentation.showRealPgDumpOperationalBanner;
  const showDevRealDumpGuidance = operatorTruth.summaryPresentation.showDevRealDumpGuidance;

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card size="small">
        <Space wrap align="center">
          <Button icon={<ReloadOutlined />} onClick={() => invalidateAll()} loading={statusQuery.isFetching}>
            {t('backupDr.actions.refresh')}
          </Button>
          {!canManage && <Typography.Text type="secondary">{t('backupDr.permission.noManage')}</Typography.Text>}
        </Space>
      </Card>

      <BackupExecutionModeCard canManage={canManage} t={t} onModeSaved={() => invalidateAll()} />

      {error && (
        <Alert
          type="error"
          showIcon
          message={t('backupDr.errors.loadFailed')}
          action={<Button onClick={() => invalidateAll()}>{t('backupDr.actions.refresh')}</Button>}
        />
      )}

      <Alert
        type="info"
        showIcon
        message={t('backupDr.scope.title')}
        description={
          <div>
            <Typography.Paragraph style={{ marginBottom: 8 }}>{t('backupDr.scope.body')}</Typography.Paragraph>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8, marginTop: 0 }}>
              {t('backupDr.scope.devRealPgLead')}
            </Typography.Paragraph>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 0, fontSize: 13 }}>
              <Typography.Link href="#backup-dr-dev-pgdump-checklist">{t('backupDr.devRealPgDump.jumpToChecklist')}</Typography.Link>
              <Typography.Text type="secondary">
                {' · '}
                {t('backupDr.devRealPgDump.docRepoFileLead')}{' '}
                <Typography.Text code copyable={{ text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH }}>
                  {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                </Typography.Text>
              </Typography.Text>
            </Typography.Paragraph>
          </div>
        }
      />

      {loading && !statusQuery.data ? (
        <Spin />
      ) : (
        <>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic
                  title={t('backupDr.summary.backupHealth')}
                  value={t(operatorTruth.summaryPresentation.backupHealthSummaryLabelKey)}
                  valueStyle={healthStatisticValueStyle(operatorTruth.summaryPresentation.backupHealthUiKind)}
                />
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                  {summaryBackupFootnote}
                </Typography.Paragraph>
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic
                  title={t('backupDr.summary.restoreReadiness')}
                  value={t(operatorTruth.summaryPresentation.restoreReadinessSummaryLabelKey)}
                  valueStyle={restoreReadinessStatisticValueStyle(operatorTruth.summaryPresentation.restoreReadinessUiKind)}
                />
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                  {summaryRestoreFootnote}
                </Typography.Paragraph>
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.configurationShort')} value={operatorTruth.summaryPresentation.configShortSummaryLine} />
                {isSimulatedAdapterEnvironment ? (
                  <Tooltip
                    title={<span style={{ whiteSpace: 'pre-wrap' }}>{t('backupDr.summary.fakeAdapterConfigNoteTooltip')}</span>}
                  >
                    <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12, cursor: 'help' }}>
                      {t('backupDr.summary.fakeAdapterConfigNote')}
                    </Typography.Paragraph>
                  </Tooltip>
                ) : null}
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.externalArchiveCard')} value={operatorTruth.artifact.externalCopyDisplayText} />
              </Card>
            </Col>
          </Row>

          {operatorTruth.executionMode.loaded ? (
            <Alert
              type="info"
              showIcon
              message={t('backupDr.summary.executionModeSurfaceTitle')}
              description={t('backupDr.summary.executionModeSurfaceBody', {
                requested: operatorTruth.executionMode.requestedUserFacingMode,
                effective: operatorTruth.executionMode.effectiveUserFacingMode,
                defaultMode: operatorTruth.executionMode.configurationDefaultUserFacingMode,
                runnable: operatorTruth.executionMode.effectiveModeRunnable ? t('common.buttons.yes') : t('common.buttons.no'),
              })}
            />
          ) : null}

          {showRealPgDumpOperationalBanner ? (
            <Alert
              type={REAL_DUMP_PATH_BANNER_ALERT_TYPE}
              showIcon
              message={t('backupDr.realDumpMode.bannerTitle')}
              description={
                <Typography.Paragraph style={{ marginBottom: 0 }}>{t('backupDr.realDumpMode.bannerBody')}</Typography.Paragraph>
              }
            />
          ) : null}

          {isSimulatedAdapterEnvironment ? (
            <Alert
              type="info"
              showIcon
              message={t('backupDr.fakeMode.bannerTitle')}
              description={
                <div>
                  <Typography.Paragraph style={{ marginBottom: 8 }}>{t('backupDr.fakeMode.bannerBody')}</Typography.Paragraph>
                  <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 13 }}>
                    {t('backupDr.fakeMode.bannerRealBackupPrereq')}
                  </Typography.Paragraph>
                  <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                    <Typography.Link href="#backup-dr-dev-pgdump-checklist">{t('backupDr.devRealPgDump.jumpToChecklist')}</Typography.Link>
                    <Typography.Text type="secondary">
                      {' · '}
                      {t('backupDr.devRealPgDump.docRepoFileLead')}{' '}
                      <Typography.Text code copyable={{ text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH }}>
                        {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                      </Typography.Text>
                    </Typography.Text>
                  </Typography.Paragraph>
                </div>
              }
            />
          ) : null}

          {operatorTruth.operatorValidity ? (
            <Alert
              type={mapOperatorValidityStripToAlertType(operatorTruth.operatorValidity.severity)}
              showIcon
              message={t(operatorTruth.operatorValidity.titleKey)}
              description={
                <Typography.Paragraph style={{ marginBottom: 0 }}>
                  {t(operatorTruth.operatorValidity.descriptionKey)}
                </Typography.Paragraph>
              }
            />
          ) : null}

          <HealthBanner
            critical={banner.critical}
            warn={banner.warn}
            info={banner.info}
            t={t}
            onRefresh={() => invalidateAll()}
          />

          <RecoverabilitySummaryCard
            summary={recoverabilityQuery.data}
            loading={recoverabilityQuery.isLoading}
            queryError={recoverabilityQuery.isError}
            onRetry={() => void recoverabilityQuery.refetch()}
            formatDt={formatDt}
            formatLocale={formatLocale}
            backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
            restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
            simulatedOperationalMode={isSimulatedAdapterEnvironment}
            omitSimulatedEnvironmentStrip={isSimulatedAdapterEnvironment}
            t={t}
          />

          <BackupEvidenceLadderCard model={operatorTruth.evidenceLadder} t={t} />

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={14}>
              <BackupRunProgressBanner
                latest={latest}
                isSimulatedExecution={operatorTruth.run.simulatedEvidence}
                recoverabilityNotProven={operatorTruth.progressRunBanner.recoverabilityNotProvenGlance}
                latestRestoreDrillFailed={operatorTruth.progressRunBanner.latestRestoreDrillFailed}
                omitSimulatedSuccessDetail={false}
                averageSucceededDurationSeconds={statusQuery.data?.averageSucceededBackupDurationSeconds ?? undefined}
                averageSucceededDurationSampleCount={statusQuery.data?.averageSucceededBackupDurationSampleCount}
                formatDt={formatDt}
                formatLocale={formatLocale}
                t={t}
              />
              <BackupStatusCard
                latest={latest}
                detail={detailForPipeline}
                policy={policy}
                loadingDetail={runDetailQuery.isFetching && Boolean(latest?.id)}
                detailError={runDetailQuery.isError}
                formatDt={formatDt}
                formatLocale={formatLocale}
                backupStatusTagColor={mapBackupRunStatusAntdColor}
                backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
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
              {latest?.id && latest.status === BackupRunResponseDtoStatus.NUMBER_3 ? (
                <BackupArtifactsDownloadCard
                  variant="latest_success"
                  runId={latest.id}
                  artifacts={detailForPipeline?.artifacts ?? []}
                  canManage={canManage}
                  isSimulatedExecution={operatorTruth.run.simulatedEvidence}
                  runAdapterKind={detailForPipeline?.adapterKind ?? undefined}
                  realPostgreSqlLogicalDumpConfigured={recoverabilityQuery.data?.realPostgreSqlLogicalDumpConfigured}
                  simulatedOperationalMode={
                    operatorTruth.simulatedOperationalMode
                  }
                  loadingArtifacts={(runDetailQuery.isLoading || runDetailQuery.isFetching) && Boolean(latest.id)}
                  t={t}
                />
              ) : null}
              {offerLastKnownGoodDownloads && lkgRunId ? (
                lkgRunDetailQuery.isError ? (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginTop: 16 }}
                    message={t('backupDr.download.lkgLoadFailed')}
                    action={
                      <Button size="small" onClick={() => void lkgRunDetailQuery.refetch()}>
                        {t('backupDr.actions.refresh')}
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
                      recoverabilityQuery.data?.lastSuccessfulBackupRunIsSimulatedExecution ??
                      undefined
                    }
                    runAdapterKind={lkgRunDetailQuery.data?.adapterKind ?? undefined}
                    realPostgreSqlLogicalDumpConfigured={recoverabilityQuery.data?.realPostgreSqlLogicalDumpConfigured}
                    simulatedOperationalMode={
                      isSimulatedBackupAdapterKind(lkgRunDetailQuery.data?.adapterKind) ||
                      lkgRunDetailQuery.data?.isSimulatedExecution === true ||
                      recoverabilityQuery.data?.lastSuccessfulBackupRunIsSimulatedExecution === true
                    }
                    loadingArtifacts={lkgRunDetailQuery.isLoading || lkgRunDetailQuery.isFetching}
                    t={t}
                  />
                )
              ) : null}
            </Col>
            <Col xs={24} lg={10}>
              <Card title={t('backupDr.externalCopy.title')} size="small">
                <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 12 }}>
                  {t('backupDr.externalCopy.scopeFromLatestRun')}
                </Typography.Paragraph>
                {runDetailQuery.isLoading && latest?.id ? (
                  <Spin tip={t('backupDr.externalCopy.loading')} />
                ) : (
                  <Alert
                    type={mapExternalCopyVariantToAlertType(operatorTruth.artifact.externalCopyVariant)}
                    showIcon
                    message={operatorTruth.artifact.externalCopyDisplayText}
                  />
                )}
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                  {t('backupDr.restoreCapability.title')}: {t(operatorTruth.restore.backendReportedCapability.labelKey)}
                </Typography.Paragraph>
              </Card>
            </Col>
          </Row>

          <Card title={t('backupDr.artifactVerification.title')} size="small">
            {!v ? (
              <Typography.Text type="secondary">{t('backupDr.artifactVerification.none')}</Typography.Text>
            ) : (
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label={t('backupDr.table.status')}>
                  <Tag color={v.status === 1 ? 'blue' : v.status === 2 ? 'error' : 'processing'}>
                    {t(`backupDr.verificationStatus.${v.status}`)}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label={t('backupDr.table.verifier')}>{v.verifierSource}</Descriptions.Item>
                <Descriptions.Item label={t('backupDr.latestRun.completed')}>
                  {formatDt(v.completedAt, formatLocale)}
                </Descriptions.Item>
              </Descriptions>
            )}
            <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
              {t('backupDr.artifactVerification.notRestoreProof')}
            </Typography.Paragraph>
          </Card>

          <Typography.Title level={4}>{t('backupDr.restoreVerification.sectionTitle')}</Typography.Title>
          <Row gutter={[16, 16]}>
            <Col xs={24} lg={12}>
              <RestoreVerificationCard
                run={rr}
                formatDt={formatDt}
                formatLocale={formatLocale}
                restoreStatusTagColor={mapRestoreVerificationStatusAntdColor}
                restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
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
              <Card title={t('backupDr.readiness.title')} size="small">
                <Alert type="info" showIcon style={{ marginBottom: 12 }} message={t('backupDr.readiness.notEndToEndDr')} />
                {!restoreReady ? (
                  <Typography.Text type="secondary">{t('backupDr.summary.unknown')}</Typography.Text>
                ) : (
                  <>
                    {normalizeHealthLevelString(restoreReady.level) !==
                    normalizeHealthLevelString(operatorTruth.restore.effectiveReadinessLevel ?? '') ? (
                      <Typography.Paragraph type="warning" style={{ marginBottom: 12 }}>
                        {t('backupDr.readiness.levelCappedForOperatorTruth')}
                      </Typography.Paragraph>
                    ) : null}
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                      {t('backupDr.readiness.backendReportedSignal')}
                    </Typography.Paragraph>
                    <Space wrap style={{ marginBottom: 12 }}>
                      <Tag
                        color={
                          operatorTruth.summaryPresentation.restoreReadinessUiKind === 'unhealthy'
                            ? 'red'
                            : operatorTruth.summaryPresentation.restoreReadinessUiKind === 'degraded'
                              ? 'orange'
                              : operatorTruth.summaryPresentation.restoreReadinessUiKind === 'healthy'
                                ? 'blue'
                                : 'default'
                        }
                      >
                        {t(operatorTruth.summaryPresentation.restoreReadinessSummaryLabelKey)}
                      </Tag>
                      <Tag color={restoreReady.workerEnabled ? 'blue' : 'orange'}>
                        {t('backupDr.readiness.restoreWorker')}: {restoreReady.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')}
                      </Tag>
                      <Tag color={restoreReady.orchestratorDistributedLockEnabled ? 'blue' : 'orange'}>
                        {t('backupDr.lock.restoreDistributedLock')}:{' '}
                        {restoreReady.orchestratorDistributedLockEnabled ? t('common.buttons.yes') : t('common.buttons.no')}
                      </Tag>
                    </Space>
                    {restoreReady.scopeDisclaimer ? (
                      <Typography.Paragraph type="secondary">{restoreReady.scopeDisclaimer}</Typography.Paragraph>
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
                      <Typography.Text type="secondary">{t('backupDr.readiness.noIssues')}</Typography.Text>
                    )}
                  </>
                )}
              </Card>
            </Col>
          </Row>

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={12}>
              <Card title={t('backupDr.pipeline.title')} size="small">
                {!policy ? (
                  <Typography.Text type="secondary">{t('backupDr.pipeline.none')}</Typography.Text>
                ) : (
                  <Descriptions column={1} size="small" bordered>
                    <Descriptions.Item label={t('backupDr.pipeline.externalRequirement')}>
                      {policy.externalArchiveRequirement ?? '—'}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('backupDr.pipeline.stagingConfigured')}>
                      {policy.artifactStagingRootConfigured === undefined
                        ? '—'
                        : policy.artifactStagingRootConfigured
                          ? t('common.buttons.yes')
                          : t('common.buttons.no')}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('backupDr.pipeline.externalConfigured')}>
                      {policy.externalArchiveRootConfigured === undefined
                        ? '—'
                        : policy.externalArchiveRootConfigured
                          ? t('common.buttons.yes')
                          : t('common.buttons.no')}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('backupDr.pipeline.runExternalWhenEligible')}>
                      {policy.willRunExternalArchiveAfterStagingVerificationWhenEligible === undefined
                        ? '—'
                        : policy.willRunExternalArchiveAfterStagingVerificationWhenEligible
                          ? t('common.buttons.yes')
                          : t('common.buttons.no')}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('backupDr.pipeline.hashReverify')}>
                      {policy.stagingOnDiskHashReverificationExpected === undefined
                        ? '—'
                        : policy.stagingOnDiskHashReverificationExpected
                          ? t('common.buttons.yes')
                          : t('common.buttons.no')}
                    </Descriptions.Item>
                    <Descriptions.Item label={t('backupDr.health.adapter')}>{policy.effectiveAdapterKind ?? '—'}</Descriptions.Item>
                  </Descriptions>
                )}
                {policy?.operatorNotes?.length ? (
                  <Typography.Paragraph style={{ marginTop: 12 }}>
                    <strong>{t('backupDr.pipeline.operatorNotes')}</strong>
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
              <Card title={t('backupDr.health.title')} size="small">
                <Space direction="vertical" style={{ width: '100%' }} size="middle">
                  <div>
                    <Tag color={tagColorForConfigurationHealthUiKind(operatorTruth.summaryPresentation.backupHealthUiKind)}>
                      {t(operatorTruth.summaryPresentation.backupHealthSummaryLabelKey)}
                    </Tag>
                    <Typography.Text type="secondary">
                      {' '}
                      {t('backupDr.health.adapter')}: {health?.effectiveAdapterKind ?? '—'} · {t('backupDr.health.worker')}:{' '}
                      {health?.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')}
                    </Typography.Text>
                  </div>
                  {health?.diagnostics && health.diagnostics.length > 0 ? (
                    <div>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {t('backupDr.health.diagnosticsIntro')}
                      </Typography.Text>
                      <div style={{ marginTop: 8 }}>
                        <Space wrap size={[4, 8]}>
                          {health.diagnostics.slice(0, 10).map((d, i) => (
                            <Tooltip key={`${d.code}-${i}`} title={d.message}>
                              <Tag color={backupDiagnosticTagColor(d.severity)}>{d.code}</Tag>
                            </Tooltip>
                          ))}
                        </Space>
                        {health.diagnostics.length > 10 ? (
                          <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
                            {t('backupDr.health.diagnosticsTruncated', { count: String(health.diagnostics.length - 10) })}
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
                    <Typography.Text type="secondary">{t('backupDr.readiness.noIssues')}</Typography.Text>
                  )}
                </Space>
              </Card>
            </Col>
          </Row>

          <Card title={t('backupDr.lock.title')} size="small">
            <Row gutter={[16, 16]}>
              <Col xs={24} md={12}>
                <Typography.Title level={5}>{t('backupDr.lock.restoreSide')}</Typography.Title>
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label={t('backupDr.lock.restoreDistributedLock')}>
                    {restoreReady?.orchestratorDistributedLockEnabled === undefined
                      ? '—'
                      : restoreReady.orchestratorDistributedLockEnabled
                        ? t('common.buttons.yes')
                        : t('common.buttons.no')}
                  </Descriptions.Item>
                  <Descriptions.Item label={t('backupDr.readiness.restoreWorker')}>
                    {restoreReady?.workerEnabled === undefined
                      ? '—'
                      : restoreReady.workerEnabled
                        ? t('common.buttons.yes')
                        : t('common.buttons.no')}
                  </Descriptions.Item>
                </Descriptions>
                {operatorTruth.lockHints.restore.length ? (
                  <Alert
                    style={{ marginTop: 12 }}
                    type="warning"
                    showIcon
                    message={t('backupDr.lock.hintsFromApi')}
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
                <Typography.Title level={5}>{t('backupDr.lock.backupSide')}</Typography.Title>
                <Typography.Paragraph type="secondary">{t('backupDr.lock.backupSideHint')}</Typography.Paragraph>
                {operatorTruth.lockHints.backup.length ? (
                  <Alert
                    type="warning"
                    showIcon
                    message={t('backupDr.lock.hintsFromApi')}
                    description={
                      <ul style={{ marginBottom: 0 }}>
                        {operatorTruth.lockHints.backup.map((x, i) => (
                          <li key={i}>{x}</li>
                        ))}
                      </ul>
                    }
                  />
                ) : (
                  <Typography.Text type="secondary">{t('backupDr.lock.noLockHints')}</Typography.Text>
                )}
              </Col>
            </Row>
          </Card>

          <Alert type="info" showIcon message={t('backupDr.knownGaps.title')} description={t('backupDr.knownGaps.body')} />

          {showDevRealDumpGuidance ? (
            <Card id="backup-dr-dev-pgdump-checklist" size="small" title={t('backupDr.devRealPgDump.title')}>
              <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
                {t('backupDr.devRealPgDump.intro')}
              </Typography.Paragraph>
              <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 8 }}>
                {t('backupDr.devRealPgDump.sectionEnableTitle')}
              </Typography.Title>
              <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.itemAdapter')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.itemStaging')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.itemConnection')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.itemBinaries')}</Typography.Text>
                </li>
              </ul>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                {t('backupDr.devRealPgDump.expectedOutputPath')}
              </Typography.Paragraph>
              <Typography.Title level={5} style={{ marginTop: 8, marginBottom: 8 }}>
                {t('backupDr.devRealPgDump.sectionTroubleshootingTitle')}
              </Typography.Title>
              <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.stuckFakeAdapter')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.stuckStaging')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.stuckConnection')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.stuckPgDump')}</Typography.Text>
                </li>
                <li>
                  <Typography.Text>{t('backupDr.devRealPgDump.stuckPgRestore')}</Typography.Text>
                </li>
              </ul>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                {t('backupDr.devRealPgDump.startupLogsHint')}
              </Typography.Paragraph>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                {t('backupDr.devRealPgDump.docHint')}
              </Typography.Paragraph>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                {t('backupDr.devRealPgDump.docRepoFileLead')}{' '}
                <Typography.Text code copyable={{ text: BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH }}>
                  {BACKUP_DEV_REAL_PG_DUMP_DOC_REPO_PATH}
                </Typography.Text>
              </Typography.Paragraph>
            </Card>
          ) : null}

          <Card title={t('backupDr.distinction.title')} size="small">
            <Typography.Paragraph>{t('backupDr.distinction.body')}</Typography.Paragraph>
          </Card>

          <Card title={t('backupDr.disclaimer.title')} size="small">
            <Typography.Paragraph>{health?.artifactVerificationDisclaimer ?? '—'}</Typography.Paragraph>
          </Card>

          <Card title={t('backupDr.alerts.title')} size="small">
            {alertItems.length === 0 ? (
              <Typography.Text type="secondary">{t('backupDr.alerts.none')}</Typography.Text>
            ) : (
              <Space direction="vertical" style={{ width: '100%' }} size="small">
                {alertItems.map((item, i) => (
                  <Alert
                    key={i}
                    type={item.severity === 'error' ? 'error' : item.severity === 'warning' ? 'warning' : 'info'}
                    showIcon
                    message={item.text}
                  />
                ))}
              </Space>
            )}
          </Card>

          <ManualActionsPanel
            canManage={canManage}
            backupTrigger={backupTrigger as ManualActionsPanelProps['backupTrigger']}
            restoreTrigger={restoreTrigger as ManualActionsPanelProps['restoreTrigger']}
            simulatedOperationalMode={isSimulatedAdapterEnvironment}
            modeAwareConfirmations={operatorTruth.manualActionsModeConfirmations}
            t={t}
          />

          <RecentRunsTable
            title={t('backupDr.runs.title')}
            rowKey="id"
            dataSource={runsQuery.data?.items ?? []}
            columns={columns}
            loading={runsQuery.isFetching}
            queryError={runsQuery.isError}
            t={t}
            onRetry={() => invalidateAll()}
          />

          <RecentRestoreDrillsTable
            title={t('backupDr.restoreHistory.title')}
            rowKey="id"
            dataSource={restoreHistoryQuery.data?.items ?? []}
            columns={restoreHistoryColumns}
            loading={restoreHistoryQuery.isFetching}
            queryError={restoreHistoryQuery.isError}
            emptyText={t('backupDr.restoreHistory.empty')}
            t={t}
            onRetry={() => invalidateAll()}
          />
        </>
      )}
    </Space>
  );
}
