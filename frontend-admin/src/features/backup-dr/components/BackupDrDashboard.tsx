'use client';

/**
 * Backup & DR panosu: salt görünürlük + yalnızca API kuyruğa alma; pipeline adımları DTO’dan türetilir.
 */

import React, { useCallback, useMemo } from 'react';
import axios from 'axios';
import { Alert, Button, Card, Col, Descriptions, Row, Space, Spin, Statistic, Tag, Typography, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { ReloadOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useI18n } from '@/i18n';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  getGetApiAdminBackupRecoverabilitySummaryQueryKey,
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
import type { BackupRunResponseDto, RestoreVerificationRunResponseDto } from '@/api/generated/model';
import { RestoreVerificationTriggerOrchestrationState } from '@/api/generated/model';
import {
  configurationHealthSummaryI18nKey,
  externalCopyVariantToI18nKey,
  mapArtifactsToExternalCopyVariant,
  mapBackupRunStatusAntdColor,
  mapDumpInspectionTriState,
  mapRestoreVerificationStatusAntdColor,
  normalizeHealthLevelString,
} from '@/features/backup-dr/logic/backupDrMappers';
import { BackupStatusCard } from '@/features/backup-dr/components/BackupStatusCard';
import { HealthBanner } from '@/features/backup-dr/components/HealthBanner';
import { ManualActionsPanel, type ManualActionsPanelProps } from '@/features/backup-dr/components/ManualActionsPanel';
import { RecentRestoreDrillsTable } from '@/features/backup-dr/components/RecentRestoreDrillsTable';
import { RecentRunsTable } from '@/features/backup-dr/components/RecentRunsTable';
import { RestoreVerificationCard } from '@/features/backup-dr/components/RestoreVerificationCard';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';
import { isBackupPipelineClientFallbackEnabled } from '@/features/backup-dr/logic/backupPipelineEnv';

function formatDt(iso: string | undefined | null, formatLocale: string): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString(formatLocale);
  } catch {
    return iso;
  }
}

function levelSummaryLabel(level: string | undefined | null, t: (k: string) => string): string {
  return t(configurationHealthSummaryI18nKey(level));
}

function backupStatusLabel(status: number | undefined, t: (k: string) => string): string {
  const s = status ?? 0;
  const key = `backupDr.backupStatus.${s}`;
  const label = t(key);
  return label === key ? String(s) : label;
}

function restoreStatusLabel(status: number | undefined, t: (k: string) => string): string {
  const s = status ?? 0;
  const key = `backupDr.restoreStatus.${s}`;
  const label = t(key);
  return label === key ? String(s) : label;
}

function summarizeExternalCopy(
  artifacts: Parameters<typeof mapArtifactsToExternalCopyVariant>[0],
  t: (k: string) => string,
) {
  const variant = mapArtifactsToExternalCopyVariant(artifacts);
  return { variant, text: t(externalCopyVariantToI18nKey(variant)) } as const;
}

function lockHintIssues(issues: string[] | undefined): string[] {
  const re = /lock|advisory|distributed|kilit|sperre/i;
  return (issues ?? []).filter((x) => re.test(x));
}

function triggerErrorMessage(err: unknown, t: (k: string) => string): string {
  if (axios.isAxiosError(err) && err.response?.status === 403) return t('backupDr.errors.forbiddenTrigger');
  return t('backupDr.errors.loadFailed');
}

export function BackupDrDashboard() {
  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const allowClientPipelineFallback = isBackupPipelineClientFallbackEnabled();

  const runsParams = useMemo(() => ({ page: 1, pageSize: 15 }), []);
  const restoreParams = useMemo(() => ({ page: 1, pageSize: 10 }), []);

  const pollBackup = useCallback((q: { state: { data?: unknown } }) => {
    const data = q.state.data as { latestRun?: { status?: number } } | undefined;
    const s = data?.latestRun?.status;
    if (s === 0 || s === 1 || s === 2) return 8_000;
    return 60_000;
  }, []);

  const pollRestore = useCallback((q: { state: { data?: unknown } }) => {
    const row = q.state.data as { status?: number } | null | undefined;
    const s = row?.status;
    if (s === 0 || s === 1) return 8_000;
    return 60_000;
  }, []);

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: pollBackup, refetchOnWindowFocus: true },
  });
  const runsQuery = useGetApiAdminBackupRuns(runsParams, {
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });
  const verificationQuery = useGetApiAdminBackupVerificationLatest({
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });
  const recoverabilityQuery = useGetApiAdminBackupRecoverabilitySummary({
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });
  const restoreLatestQuery = useGetApiAdminRestoreVerificationRunsLatest({
    query: { refetchInterval: pollRestore, refetchOnWindowFocus: true },
  });
  const restoreHistoryQuery = useGetApiAdminRestoreVerificationRuns(restoreParams, {
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });
  const restoreReadinessQuery = useGetApiAdminRestoreVerificationReadiness({
    query: { refetchInterval: 60_000, refetchOnWindowFocus: true },
  });

  const latest = statusQuery.data?.latestRun;
  const policy = statusQuery.data?.artifactPipelinePolicy;
  const runDetailQuery = useGetApiAdminBackupRunsId(latest?.id ?? '', {
    query: {
      enabled: Boolean(latest?.id),
      refetchOnWindowFocus: true,
    },
  });

  const invalidateReadiness = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: getGetApiAdminRestoreVerificationReadinessQueryKey() });
  }, [queryClient]);

  const backupTrigger = usePostApiAdminBackupTrigger({
    mutation: {
      onSuccess: async (res) => {
        if (res.duplicateExecutionPrevented) message.info(t('backupDr.messages.backupDuplicate'));
        else message.success(t('backupDr.messages.backupEnqueued'));
        if (res.orchestrationState)
          message.info(t('backupDr.messages.orchestration', { state: res.orchestrationState }));
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
    await queryClient.invalidateQueries({ queryKey: getGetApiAdminBackupRecoverabilitySummaryQueryKey() });
    await queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
  }, [queryClient]);

  const health = statusQuery.data?.configurationHealth;
  const healthLv = normalizeHealthLevelString(health?.level);
  const restoreReady = restoreReadinessQuery.data;
  const restoreLv = normalizeHealthLevelString(restoreReady?.level);

  const detailForPipeline = runDetailQuery.data ?? null;
  const external = summarizeExternalCopy(detailForPipeline?.artifacts, t);

  const backupLockHints = useMemo(() => lockHintIssues(health?.issues), [health?.issues]);
  const restoreLockHints = useMemo(() => lockHintIssues(restoreReady?.issues), [restoreReady?.issues]);

  const banner = useMemo(() => {
    const critical: string[] = [];
    const warn: string[] = [];

    if (healthLv === 'unhealthy' && !(health?.issues?.length))
      critical.push(t('backupDr.banner.backupConfigUnhealthy'));
    else if (healthLv === 'degraded' && !(health?.issues?.length))
      warn.push(t('backupDr.banner.backupConfigDegraded'));

    if (restoreLv === 'unhealthy' && !(restoreReady?.issues?.length))
      critical.push(t('backupDr.banner.restoreReadinessUnhealthy'));
    else if (restoreLv === 'degraded' && !(restoreReady?.issues?.length))
      warn.push(t('backupDr.banner.restoreReadinessDegraded'));

    if (health && !health.workerEnabled) critical.push(t('backupDr.health.workerDisabled'));

    if (health && health.realPostgreSqlLogicalDumpConfigured === false) {
      const n = health.readinessNarrative?.trim();
      warn.push(
        n ? `${t('backupDr.banner.noRealPostgreSqlBackup')}: ${n}` : t('backupDr.banner.noRealPostgreSqlBackup'),
      );
    }

    if (restoreReady && !restoreReady.workerEnabled) warn.push(t('backupDr.readiness.restoreWorkerDisabled'));

    if (restoreReady?.orchestratorDistributedLockEnabled === false) warn.push(t('backupDr.lock.restoreLockDisabled'));

    const lr = latest;
    if (lr?.status === 4 || lr?.status === 5) {
      critical.push(
        `${t('backupDr.latestRun.failure')}: ${lr.failureCode ?? '—'} — ${(lr.failureDetail ?? '').trim()}`.trim(),
      );
    }

    const v = verificationQuery.data;
    if (v && v.status === 2 && v.failureReason) {
      warn.push(`${t('backupDr.artifactVerification.failed')}: ${v.failureReason}`);
    }

    if (external.variant === 'failed' || external.variant === 'mixed') {
      warn.push(t('backupDr.banner.externalArchiveDegraded'));
    }

    const rr = restoreLatestQuery.data;
    if (rr && rr.status === 3) {
      critical.push(
        `${t('backupDr.restoreVerification.drillFailed')}: ${rr.failureCode ?? ''} ${(rr.failureDetail ?? '').trim()}`.trim(),
      );
    }

    for (const issue of health?.issues ?? []) {
      if (healthLv === 'unhealthy') {
        if (!critical.includes(issue)) critical.push(issue);
      } else if (healthLv === 'degraded') {
        if (!warn.includes(issue)) warn.push(issue);
      }
    }
    for (const issue of restoreReady?.issues ?? []) {
      if (restoreLv === 'unhealthy') {
        if (!critical.includes(issue)) critical.push(issue);
      } else if (restoreLv === 'degraded') {
        if (!warn.includes(issue)) warn.push(issue);
      }
    }

    return { critical, warn };
  }, [
    external.variant,
    health,
    healthLv,
    latest,
    restoreLatestQuery.data,
    restoreLv,
    restoreReady,
    t,
    verificationQuery.data,
  ]);

  const alertItems = useMemo(() => {
    const items: { severity: 'error' | 'warning' | 'info'; text: string }[] = [];
    for (const issue of health?.issues ?? []) {
      items.push({
        severity: healthLv === 'unhealthy' ? 'error' : 'warning',
        text: issue,
      });
    }
    if (health && !health.workerEnabled) {
      items.push({ severity: 'error', text: t('backupDr.health.workerDisabled') });
    }
    if (health && health.realPostgreSqlLogicalDumpConfigured === false) {
      const n = health.readinessNarrative?.trim();
      items.push({
        severity: 'warning',
        text: n
          ? `${t('backupDr.banner.noRealPostgreSqlBackup')}: ${n}`
          : t('backupDr.banner.noRealPostgreSqlBackup'),
      });
    }
    const lr = latest;
    if (lr?.status === 4 || lr?.status === 5) {
      items.push({
        severity: 'error',
        text: `${t('backupDr.latestRun.failure')}: ${lr.failureCode ?? '—'} — ${lr.failureDetail ?? ''}`.trim(),
      });
    }
    const v = verificationQuery.data;
    if (v && v.status === 2 && v.failureReason) {
      items.push({
        severity: 'warning',
        text: `${t('backupDr.artifactVerification.failed')}: ${v.failureReason}`,
      });
    }
    const rr = restoreLatestQuery.data;
    if (rr && rr.status === 3) {
      items.push({
        severity: 'error',
        text: `Restore drill: ${rr.failureCode ?? ''} ${rr.failureDetail ?? ''}`.trim(),
      });
    }
    for (const issue of restoreReady?.issues ?? []) {
      items.push({
        severity: restoreLv === 'unhealthy' ? 'error' : 'warning',
        text: issue,
      });
    }
    if (restoreReady && !restoreReady.workerEnabled) {
      items.push({ severity: 'warning', text: t('backupDr.readiness.restoreWorkerDisabled') });
    }
    const restoreNotes = statusQuery.data?.restore?.notes?.trim();
    if (restoreNotes) items.push({ severity: 'info', text: restoreNotes });
    return items;
  }, [health, healthLv, latest, restoreLatestQuery.data, restoreLv, restoreReady, statusQuery.data?.restore?.notes, t, verificationQuery.data]);

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
          <Tag color={mapBackupRunStatusAntdColor(s)}>{backupStatusLabel(s, t)}</Tag>
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
    [formatLocale, t],
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
        render: (s: number | undefined) => (
          <Tag color={mapRestoreVerificationStatusAntdColor(s)}>{restoreStatusLabel(s, t)}</Tag>
        ),
      },
      {
        title: t('backupDr.table.dumpInspection'),
        key: 'dump',
        render: (_: unknown, row) => {
          const p = mapDumpInspectionTriState(row);
          if (p === undefined) return '—';
          return p ? t('backupDr.triState.ok') : t('backupDr.triState.fail');
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
        render: (c: string | null | undefined) => c ?? '—',
      },
    ],
    [formatLocale, t],
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

  const v = verificationQuery.data;
  const rr = restoreLatestQuery.data;

  const summaryBackupHealth = levelSummaryLabel(health?.level, t);
  const summaryRestoreHealth = levelSummaryLabel(restoreReady?.level, t);
  const summaryConfigShort = `${t('backupDr.health.adapter')}: ${health?.effectiveAdapterKind ?? '—'} · ${t('backupDr.health.worker')}: ${
    health?.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')
  } · ${
    health?.realPostgreSqlLogicalDumpConfigured
      ? t('backupDr.health.realPgDumpYes')
      : t('backupDr.health.realPgDumpNo')
  }`;

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

      {error && (
        <Alert
          type="error"
          showIcon
          message={t('backupDr.errors.loadFailed')}
          action={<Button onClick={() => invalidateAll()}>{t('backupDr.actions.refresh')}</Button>}
        />
      )}

      <Alert type="info" showIcon message={t('backupDr.scope.title')} description={t('backupDr.scope.body')} />

      {loading && !statusQuery.data ? (
        <Spin />
      ) : (
        <>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.backupHealth')} value={summaryBackupHealth} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.restoreReadiness')} value={summaryRestoreHealth} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.configurationShort')} value={summaryConfigShort} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={t('backupDr.summary.externalArchiveCard')} value={external.text} />
              </Card>
            </Col>
          </Row>

          <HealthBanner critical={banner.critical} warn={banner.warn} t={t} onRefresh={() => invalidateAll()} />

          <RecoverabilitySummaryCard
            summary={recoverabilityQuery.data}
            loading={recoverabilityQuery.isLoading}
            formatDt={formatDt}
            formatLocale={formatLocale}
            backupStatusLabel={backupStatusLabel}
            restoreStatusLabel={restoreStatusLabel}
            t={t}
          />

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={14}>
              <BackupStatusCard
                latest={latest}
                detail={detailForPipeline}
                policy={policy}
                loadingDetail={runDetailQuery.isFetching && Boolean(latest?.id)}
                detailError={runDetailQuery.isError}
                formatDt={formatDt}
                formatLocale={formatLocale}
                backupStatusTagColor={mapBackupRunStatusAntdColor}
                backupStatusLabel={backupStatusLabel}
                allowClientPipelineFallback={allowClientPipelineFallback}
                t={t}
              />
            </Col>
            <Col xs={24} lg={10}>
              <Card title={t('backupDr.externalCopy.title')} size="small">
                {runDetailQuery.isLoading && latest?.id ? (
                  <Spin tip={t('backupDr.externalCopy.loading')} />
                ) : (
                  <Alert
                    type={external.variant === 'failed' || external.variant === 'mixed' ? 'warning' : 'info'}
                    showIcon
                    message={external.text}
                  />
                )}
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                  {t('backupDr.restoreCapability.title')}:{' '}
                  {statusQuery.data?.restore?.isAutomatedRestoreAvailable
                    ? t('backupDr.restoreCapability.available')
                    : t('backupDr.restoreCapability.notAvailable')}
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
                  <Tag color={v.status === 1 ? 'success' : v.status === 2 ? 'error' : 'processing'}>
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
                restoreStatusLabel={restoreStatusLabel}
                dumpInspectionTriState={mapDumpInspectionTriState}
                t={t}
              />
            </Col>
            <Col xs={24} lg={12}>
              <Card title={t('backupDr.readiness.title')} size="small">
                {!restoreReady ? (
                  <Typography.Text type="secondary">{t('backupDr.summary.unknown')}</Typography.Text>
                ) : (
                  <>
                    <Space wrap style={{ marginBottom: 12 }}>
                      <Tag color={restoreLv === 'unhealthy' ? 'red' : restoreLv === 'degraded' ? 'orange' : 'green'}>
                        {levelSummaryLabel(restoreReady.level, t)}
                      </Tag>
                      <Tag color={restoreReady.workerEnabled ? 'green' : 'orange'}>
                        {t('backupDr.readiness.restoreWorker')}: {restoreReady.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')}
                      </Tag>
                      <Tag color={restoreReady.orchestratorDistributedLockEnabled ? 'green' : 'orange'}>
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
                    <Tag color={healthLv === 'unhealthy' ? 'red' : healthLv === 'degraded' ? 'orange' : 'green'}>
                      {summaryBackupHealth}
                    </Tag>
                    <Typography.Text type="secondary">
                      {' '}
                      {t('backupDr.health.adapter')}: {health?.effectiveAdapterKind ?? '—'} · {t('backupDr.health.worker')}:{' '}
                      {health?.workerEnabled ? t('common.buttons.yes') : t('common.buttons.no')}
                    </Typography.Text>
                  </div>
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
                {restoreLockHints.length ? (
                  <Alert
                    style={{ marginTop: 12 }}
                    type="warning"
                    showIcon
                    message={t('backupDr.lock.hintsFromApi')}
                    description={
                      <ul style={{ marginBottom: 0 }}>
                        {restoreLockHints.map((x, i) => (
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
                {backupLockHints.length ? (
                  <Alert
                    type="warning"
                    showIcon
                    message={t('backupDr.lock.hintsFromApi')}
                    description={
                      <ul style={{ marginBottom: 0 }}>
                        {backupLockHints.map((x, i) => (
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
