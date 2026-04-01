'use client';

/**
 * Son istek durumu ile son bilinen iyi yedek / doğrulama / restore kanıtını ayırır (API recoverability-summary).
 */

import React from 'react';
import { Alert, Button, Card, Descriptions, Spin, Tag, Typography } from 'antd';
import type { BackupRecoverabilitySummaryResponseDto } from '@/api/generated/model';
import {
  mapBackupRunStatusAntdColor,
  mapRestoreVerificationStatusAntdColor,
} from '@/features/backup-dr/logic/backupDrMappers';

export interface RecoverabilitySummaryCardProps {
  summary: BackupRecoverabilitySummaryResponseDto | undefined;
  loading: boolean;
  queryError?: boolean;
  onRetry?: () => void;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  backupStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  t: (k: string) => string;
}

function formatAgeSeconds(sec: number | null | undefined): string {
  if (sec == null || Number.isNaN(sec)) return '—';
  return `${sec} s`;
}

export function RecoverabilitySummaryCard({
  summary,
  loading,
  queryError = false,
  onRetry,
  formatDt,
  formatLocale,
  backupStatusLabel,
  restoreStatusLabel,
  t,
}: RecoverabilitySummaryCardProps) {
  return (
    <Card title={t('backupDr.recoverability.title')} size="small">
      <Typography.Paragraph type="secondary" style={{ marginTop: 0, marginBottom: 12 }}>
        {t('backupDr.recoverability.subtitle')}
      </Typography.Paragraph>
      {queryError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('backupDr.errors.recoverabilityLoadFailed')}
          action={
            onRetry ? (
              <Button size="small" onClick={onRetry}>
                {t('backupDr.actions.refresh')}
              </Button>
            ) : undefined
          }
        />
      ) : null}
      {loading && !summary ? (
        <Spin />
      ) : !summary ? (
        <Typography.Text type="secondary">{t('backupDr.summary.unknown')}</Typography.Text>
      ) : (
        <>
        <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
          <Descriptions.Item label={t('backupDr.recoverability.executionProfile')} span={2}>
            <Typography.Text code>{summary.backupExecutionReality ?? '—'}</Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.realPgDumpConfigured')} span={2}>
            {summary.realPostgreSqlLogicalDumpConfigured ? t('common.buttons.yes') : t('common.buttons.no')}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.readinessLevel')} span={2}>
            {summary.backupReadinessLevel ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.serverNarrative')} span={2}>
            <Typography.Text type="secondary" style={{ whiteSpace: 'pre-wrap' }}>
              {summary.backupReadinessNarrative?.trim() || '—'}
            </Typography.Text>
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.lastGoodBackup')} span={2}>
            {formatDt(summary.lastSuccessfulBackupAt, formatLocale)}
            {summary.lastSuccessfulBackupRunId ? (
              <Typography.Text type="secondary" style={{ marginLeft: 8 }} copyable>
                {summary.lastSuccessfulBackupRunId}
              </Typography.Text>
            ) : null}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.backupProofAge')} span={2}>
            {formatAgeSeconds(summary.backupProofAgeSeconds)}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.lastGoodArtifactVerification')} span={2}>
            {formatDt(summary.lastSuccessfulArtifactVerificationAt, formatLocale)}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.lastGoodRestoreProof')} span={2}>
            {formatDt(summary.lastSuccessfulRestoreProofAt, formatLocale)}
            {summary.lastSuccessfulRestoreProofRunId ? (
              <Typography.Text type="secondary" style={{ marginLeft: 8 }} copyable>
                {summary.lastSuccessfulRestoreProofRunId}
              </Typography.Text>
            ) : null}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.restoreProofAge')} span={2}>
            {formatAgeSeconds(summary.restoreProofAgeSeconds)}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.latestBackupRequest')} span={2}>
            {formatDt(summary.latestRunAt, formatLocale)}{' '}
            {summary.latestRunStatus !== undefined && summary.latestRunStatus !== null ? (
              <Tag color={mapBackupRunStatusAntdColor(summary.latestRunStatus)}>
                {backupStatusLabel(summary.latestRunStatus, t)}
              </Tag>
            ) : (
              '—'
            )}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.recoverability.latestRestoreRequest')} span={2}>
            {formatDt(summary.latestRestoreRunAt, formatLocale)}{' '}
            {summary.latestRestoreRunStatus !== undefined && summary.latestRestoreRunStatus !== null ? (
              <Tag color={mapRestoreVerificationStatusAntdColor(summary.latestRestoreRunStatus)}>
                {restoreStatusLabel(summary.latestRestoreRunStatus, t)}
              </Tag>
            ) : (
              '—'
            )}
          </Descriptions.Item>
        </Descriptions>
        {summary.lastSuccessfulBackupRunIsSimulatedExecution === true ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginTop: 12 }}
            message={t('backupDr.recoverability.lastGoodBackupSimulatedWarning')}
          />
        ) : null}
        </>
      )}
    </Card>
  );
}
