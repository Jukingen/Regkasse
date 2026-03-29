'use client';

/**
 * Son yedek özeti: API durumu + pipeline adımları; storageLocator/path gösterilmez.
 */

import React, { useMemo } from 'react';
import { Alert, Card, Descriptions, Space, Spin, Tag, Typography } from 'antd';
import type { BackupArtifactPipelinePolicyResponseDto, BackupRunResponseDto } from '@/api/generated/model';
import { BackupPipelineStepper } from '@/features/backup-dr/components/BackupPipelineStepper';
import {
  formatRunDurationMs,
  resolveBackupPipelineStepsForUi,
  sumLogicalDumpBytes,
} from '@/features/backup-dr/logic/backupPipelineDerived';

export interface BackupStatusCardProps {
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
}

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

export function BackupStatusCard({
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
}: BackupStatusCardProps) {
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

  return (
    <Card title={t('backupDr.latestRun.title')} size="small">
      {!latest ? (
        <Typography.Text type="secondary">{t('backupDr.latestRun.none')}</Typography.Text>
      ) : (
        <>
          <Alert type="info" showIcon message={t('backupDr.latestRun.orchestrationHint')} style={{ marginBottom: 12 }} />
          <Descriptions column={1} size="small" bordered style={{ marginBottom: 16 }}>
            <Descriptions.Item label={t('backupDr.latestRun.id')}>{latest.id}</Descriptions.Item>
            <Descriptions.Item label={t('backupDr.table.status')}>
              <Tag color={backupStatusTagColor(latest.status ?? -1)}>{backupStatusLabel(latest.status, t)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.adapter')}>{latest.adapterKind}</Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.duration')}>{formatDurationMs(durationMs, t)}</Descriptions.Item>
            <Descriptions.Item label={t('backupDr.latestRun.dumpSize')}>{formatBytes(bytes, t)}</Descriptions.Item>
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
              <Tag color="success">{t('backupDr.pipelineSteps.sourceBadge.serverProjection')}</Tag>
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
              message={
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
              message={
                projectionVersionMismatch
                  ? t('backupDr.pipelineSteps.sourceNotice.projectionVersionBlocked')
                  : t('backupDr.pipelineSteps.sourceNotice.fallbackDisabled')
              }
            />
          )}
          {detailError && (
            <Alert type="warning" showIcon style={{ marginBottom: 12 }} message={t('backupDr.errors.runDetailPartial')} />
          )}
          {loadingDetail && latest.id ? (
            <Spin tip={t('backupDr.externalCopy.loading')} />
          ) : (
            <BackupPipelineStepper steps={steps} t={t} />
          )}
        </>
      )}
    </Card>
  );
}
