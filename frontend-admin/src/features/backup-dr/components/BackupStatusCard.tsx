'use client';

/**
 * Son yedek özeti: API durumu + pipeline adımları; storageLocator/path gösterilmez.
 * `BackupStatusCard`: GET status/latest + run-by-id with legacy polling timings.
 */

import React, { useMemo } from 'react';
import { Alert, Card, Descriptions, Space, Spin, Tag, Typography } from 'antd';
import type { BackupArtifactPipelinePolicyResponseDto, BackupRunResponseDto } from '@/api/generated/model';
import {
  useGetApiAdminBackupRunsId,
  useGetApiAdminBackupStatusLatest,
} from '@/api/generated/admin-backup/admin-backup';
import { BackupPipelineStepper } from '@/features/backup-dr/components/BackupPipelineStepper';
import {
  formatRunDurationMs,
  resolveBackupPipelineStepsForUi,
  sumLogicalDumpBytes,
} from '@/features/backup-dr/logic/backupPipelineDerived';
import type { RunTruth } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import { isSimulatedBackupAdapterKind } from '@/features/backup-dr/logic/backupDrMappers';
import {
  usePollBackupLatestDashboardInterval,
  usePollRunDetailDashboardInterval,
} from '@/features/backup-dr/logic/backupDashboardQueryTiming';
import { apiNullableToUndefined } from '@/features/backup-dr/logic/backupDrDtoNormalize';

export interface BackupLatestRunCardPresentationProps {
  latest: BackupRunResponseDto | undefined | null;
  detail: BackupRunResponseDto | undefined | null;
  policy: BackupArtifactPipelinePolicyResponseDto | undefined;
  loadingDetail: boolean;
  detailError: boolean;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  backupStatusTagColor: (status: number) => string;
  backupStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  t: (key: string, options?: Record<string, string | number>) => string;
  /**
   * false: istemci türetimi kapalı (NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK !== "true").
   * Sunucu projeksiyonu geçerliyse her zaman kullanılır.
   */
  allowClientPipelineFallback?: boolean;
  /** recoverability-summary kartından bu kartın ayrı olduğunu vurgular (yanıltıcı birleşimi önler). */
  showLatestRunVsRecoverabilityHint?: boolean;
  /** Fake/Stub ortamı — “başarılı” çalıştırma gerçek yedek anlamına gelmez. */
  simulatedOperationalMode?: boolean;
  /** Üst pano Fake uyarısı varken kart içi tekrar uyarıyı keser. */
  omitFakeOperationalNotice?: boolean;
  /** Merkezi operatör-doğruluk modeli — verilirse simülasyon/başarı etiketleri buradan türetilir. */
  operatorRunTruth?: Pick<RunTruth, 'technicalSuccess' | 'simulatedEvidence'>;
}

/** @deprecated Prefer `BackupLatestRunCardPresentationProps` for unit tests/pure rendering. */
export type BackupStatusCardProps = BackupLatestRunCardPresentationProps;

export type BackupStatusCardOrchestrationProps = Omit<
  BackupLatestRunCardPresentationProps,
  'latest' | 'detail' | 'policy' | 'loadingDetail' | 'detailError'
>;

function formatDurationMs(ms: number | undefined, t: (key: string, options?: Record<string, string | number>) => string): string {
  if (ms === undefined) return '—';
  if (ms < 1000) return t('backupDr.latestRun.durationMs', { ms: String(Math.round(ms)) });
  const s = Math.round(ms / 1000);
  if (s < 120) return t('backupDr.latestRun.durationSec', { s: String(s) });
  const m = Math.floor(s / 60);
  const rs = s % 60;
  return t('backupDr.latestRun.durationMin', { m: String(m), s: String(rs) });
}

function formatBytes(n: number | undefined, t: (key: string, options?: Record<string, string | number>) => string): string {
  if (n === undefined) return '—';
  if (n < 1024) return t('backupDr.latestRun.bytesB', { n: String(n) });
  const kb = n / 1024;
  if (kb < 1024) return t('backupDr.latestRun.bytesKb', { n: kb.toFixed(1) });
  const mb = kb / 1024;
  return t('backupDr.latestRun.bytesMb', { n: mb.toFixed(2) });
}

export function BackupLatestRunCardPresentation({
  latest,
  detail,
  policy,
  loadingDetail,
  detailError,
  formatDt,
  formatLocale,
  backupStatusTagColor,
  backupStatusLabel,
  t,
  allowClientPipelineFallback = false,
  showLatestRunVsRecoverabilityHint = false,
  simulatedOperationalMode = false,
  omitFakeOperationalNotice = false,
  operatorRunTruth,
}: BackupLatestRunCardPresentationProps) {
  const allowFb = allowClientPipelineFallback === true;
  const resolved = useMemo(
    () =>
      resolveBackupPipelineStepsForUi(latest, detail, policy, {
        allowClientFallback: allowFb,
      }),
    [latest, detail, policy, allowFb],
  );
  const { steps, source, projectionVersionMismatch } = resolved;
  const durationMs = formatRunDurationMs(latest?.requestedAt, latest?.completedAt);
  const bytes = sumLogicalDumpBytes(detail?.artifacts ?? latest?.artifacts ?? undefined);

  const succeededSimulated = operatorRunTruth
    ? operatorRunTruth.technicalSuccess && operatorRunTruth.simulatedEvidence
    : latest?.status === 3 &&
      (detail?.isSimulatedExecution === true || isSimulatedBackupAdapterKind(latest?.adapterKind));
  const statusTagColor = succeededSimulated ? 'blue' : backupStatusTagColor(latest?.status ?? -1);
  const statusLabelText =
    succeededSimulated && latest
      ? t('backupDr.backupStatus.simulatedSuccess')
      : backupStatusLabel(latest?.status, t);

  return (
    <Card title={t('backupDr.latestRun.title')} size="small">
      {!latest ? (
        <Typography.Text type="secondary">{t('backupDr.latestRun.none')}</Typography.Text>
      ) : (
        <>
          {showLatestRunVsRecoverabilityHint ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
              {t('backupDr.latestRun.distinctFromRecoverability')}
            </Typography.Paragraph>
          ) : null}
          <Alert type="info" showIcon title={t('backupDr.latestRun.orchestrationHint')} style={{ marginBottom: 12 }} />
          {simulatedOperationalMode && !omitFakeOperationalNotice ? (
            <Alert type="info" showIcon style={{ marginBottom: 12 }} title={t('backupDr.latestRun.fakeModeOperationalNotice')} />
          ) : null}
          <Descriptions column={1} size="small" bordered style={{ marginBottom: 16 }}>
            <Descriptions.Item label={t('backupDr.latestRun.id')}>{latest.id}</Descriptions.Item>
            <Descriptions.Item label={t('backupDr.table.status')}>
              <Space wrap>
                <Tag color={statusTagColor}>{statusLabelText}</Tag>
                {succeededSimulated ? (
                  <Tag color="blue">{t('backupDr.latestRun.simulatedBadge')}</Tag>
                ) : null}
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.adapter')}>{latest.adapterKind}</Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.duration')}>{formatDurationMs(durationMs, t)}</Descriptions.Item>
            <Descriptions.Item label={t(succeededSimulated ? 'backupDr.latestRun.dumpSizeStub' : 'backupDr.latestRun.dumpSize')}>
              {formatBytes(bytes, t)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.requested')}>
              {formatDt(latest.requestedAt, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.started')}>
              {formatDt(latest.startedAt, formatLocale)}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.completed')}>
              {formatDt(latest.completedAt, formatLocale)}
            </Descriptions.Item>
          </Descriptions>
          <Space align="center" wrap style={{ marginBottom: 8 }} size="middle">
            <Typography.Title level={5} style={{ margin: 0 }}>
              {t('backupDr.pipelineSteps.sectionTitle')}
            </Typography.Title>
            {source === 'server_projection' && (
              <Tag color="blue">{t('backupDr.pipelineSteps.sourceBadge.serverProjection')}</Tag>
            )}
            {source === 'client_fallback' && (
              <Tag color="warning">{t('backupDr.pipelineSteps.sourceBadge.clientDerived')}</Tag>
            )}
            {source === 'client_fallback_blocked' && (
              <Tag color="default">{t('backupDr.pipelineSteps.sourceBadge.noProjection')}</Tag>
            )}
          </Space>
          {source === 'client_fallback' && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              title={
                projectionVersionMismatch
                  ? t('backupDr.pipelineSteps.sourceNotice.projectionVersionMismatch', {
                      version: (detail?.pipeline ?? latest?.pipeline)?.projectionVersion ?? '—',
                    })
                  : t('backupDr.pipelineSteps.sourceNotice.clientDerived')
              }
            />
          )}
          {source === 'client_fallback_blocked' && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              title={
                projectionVersionMismatch
                  ? t('backupDr.pipelineSteps.sourceNotice.projectionVersionBlocked')
                  : t('backupDr.pipelineSteps.sourceNotice.fallbackDisabled')
              }
            />
          )}
          {detailError && (
            <Alert type="warning" showIcon style={{ marginBottom: 12 }} title={t('backupDr.errors.runDetailPartial')} />
          )}
          {loadingDetail && latest.id ? (
            <Spin description={t('backupDr.externalCopy.loading')} />
          ) : (
            <BackupPipelineStepper steps={steps} t={t} />
          )}
        </>
      )}
    </Card>
  );
}

/** Latest backup snapshot + pipeline: owns `GET /api/admin/backup/status/latest` and run detail polling. */
export function BackupStatusCard(props: BackupStatusCardOrchestrationProps) {
  const pollBackup = usePollBackupLatestDashboardInterval();
  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: pollBackup, refetchOnWindowFocus: true },
  });
  const latest = apiNullableToUndefined(statusQuery.data?.latestRun);
  const policy = statusQuery.data?.artifactPipelinePolicy;
  const pollRunDetail = usePollRunDetailDashboardInterval(latest?.id, latest?.status);

  const runDetailQuery = useGetApiAdminBackupRunsId(latest?.id ?? '', {
    query: {
      enabled: Boolean(latest?.id),
      refetchInterval: pollRunDetail,
      refetchOnWindowFocus: true,
    },
  });
  const detailForPipeline = runDetailQuery.data ?? null;

  return (
    <BackupLatestRunCardPresentation
      latest={latest}
      detail={detailForPipeline}
      policy={policy}
      loadingDetail={runDetailQuery.isFetching && Boolean(latest?.id)}
      detailError={runDetailQuery.isError}
      {...props}
    />
  );
}
