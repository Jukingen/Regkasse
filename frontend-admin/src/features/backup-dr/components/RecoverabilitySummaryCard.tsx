'use client';

/**
 * Kurtarılabilirlik: kanıt (son bilinen iyi) ile son istek durumu görsel ve anlamsal ayrılmıştır; yaş/tazelik etiketli.
 */

import React from 'react';
import { Alert, Button, Card, Descriptions, Divider, Spin, Tag, Typography } from 'antd';
import type { BackupRecoverabilitySummaryResponseDto } from '@/api/generated/model';
import { BackupRecoverabilitySummaryResponseDtoLatestRestoreRunStatus } from '@/api/generated/model';
import {
  formatRecoverabilityTimestampOrProofGap,
  hasRecoverabilityProofGaps,
} from '@/features/backup-dr/logic/backupDrOperatorTruth';
import {
  freshnessTagColor,
  proofAgeFreshnessTier,
  type ProofAgeFreshnessTier,
} from '@/features/backup-dr/logic/recoverabilityPresentation';

export interface RecoverabilitySummaryCardProps {
  summary: BackupRecoverabilitySummaryResponseDto | undefined;
  loading: boolean;
  queryError?: boolean;
  onRetry?: () => void;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  backupStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  /** Fake/Stub adaptör veya simüle çalıştırma — kanıt bloklarında dil sıkılaştırılır. */
  simulatedOperationalMode?: boolean;
  /**
   * Pano üstünde zaten `backupDr.fakeMode` uyarısı varken `simulatedEnvironmentStrip` tekrarını keser;
   * kanıt bloğu / compact simulated metinleri kalır.
   */
  omitSimulatedEnvironmentStrip?: boolean;
  t: (k: string) => string;
}

function formatAgeSeconds(sec: number | null | undefined): string {
  if (sec == null || Number.isNaN(sec)) return '—';
  return `${sec} s`;
}

function FreshnessFootnote({ tier, t }: { tier: ProofAgeFreshnessTier; t: (k: string) => string }) {
  const key =
    tier === 'recent'
      ? 'backupDr.recoverability.freshnessFootnote.recent'
      : tier === 'aging'
        ? 'backupDr.recoverability.freshnessFootnote.aging'
        : tier === 'stale'
          ? 'backupDr.recoverability.freshnessFootnote.stale'
          : 'backupDr.recoverability.freshnessFootnote.unknown';
  return (
    <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
      {t(key)}
    </Typography.Text>
  );
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
  simulatedOperationalMode = false,
  omitSimulatedEnvironmentStrip = false,
  t,
}: RecoverabilitySummaryCardProps) {
  const simulatedLkg = summary?.lastSuccessfulBackupRunIsSimulatedExecution === true;
  /** Banner + özet satırlar zaten “pg_dump yok / sahte” anlatıyorsa büyük sarı kutuyu tekrarlama. */
  const compactSimulatedProof =
    simulatedLkg && summary?.realPostgreSqlLogicalDumpConfigured === false;

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
          {hasRecoverabilityProofGaps(summary) ? (
            <Alert type="info" showIcon style={{ marginBottom: 12 }} message={t('backupDr.operatorTruth.recoverabilityProofGap')} />
          ) : null}

          {summary.latestRestoreRunStatus === BackupRecoverabilitySummaryResponseDtoLatestRestoreRunStatus.NUMBER_3 &&
          summary.lastSuccessfulRestoreProofAt ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message={t('backupDr.recoverability.latestDrillFailedVsProofTimestamps')}
            />
          ) : null}

          {simulatedOperationalMode && !omitSimulatedEnvironmentStrip ? (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message={t('backupDr.recoverability.simulatedEnvironmentStrip')}
            />
          ) : null}

          <div
            style={{
              borderLeft: '4px solid #1677ff',
              padding: 12,
              marginBottom: 16,
              borderRadius: 4,
              background: '#f0f5ff',
            }}
          >
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {t('backupDr.recoverability.proofSectionTitle')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
              {t('backupDr.recoverability.proofSectionIntro')}
            </Typography.Paragraph>

            {simulatedLkg && compactSimulatedProof ? (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                {t('backupDr.recoverability.simulatedProofCompact')}
              </Typography.Paragraph>
            ) : null}
            {simulatedLkg && !compactSimulatedProof ? (
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 12 }}
                message={t('backupDr.recoverability.lastGoodBackupSimulatedWarning')}
              />
            ) : null}

            <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
              <Descriptions.Item
                label={t(simulatedLkg ? 'backupDr.recoverability.proofBlock.backupStub' : 'backupDr.recoverability.proofBlock.backup')}
                span={2}
              >
                <Typography.Text strong>
                  {formatRecoverabilityTimestampOrProofGap(summary.lastSuccessfulBackupAt, formatDt, formatLocale, t)}
                </Typography.Text>
                {summary.lastSuccessfulBackupRunId ? (
                  <Typography.Text type="secondary" style={{ marginLeft: 8 }} copyable>
                    {summary.lastSuccessfulBackupRunId}
                  </Typography.Text>
                ) : null}
                <div style={{ marginTop: 8 }}>
                  <Typography.Text type="secondary">{t('backupDr.recoverability.backupProofAge')}: </Typography.Text>
                  <Tag color={freshnessTagColor(proofAgeFreshnessTier(summary.backupProofAgeSeconds))}>
                    {formatAgeSeconds(summary.backupProofAgeSeconds)}
                  </Tag>
                  <FreshnessFootnote tier={proofAgeFreshnessTier(summary.backupProofAgeSeconds)} t={t} />
                </div>
              </Descriptions.Item>

              <Descriptions.Item label={t('backupDr.recoverability.proofBlock.artifactVerification')} span={2}>
                <Typography.Text strong>
                  {formatRecoverabilityTimestampOrProofGap(
                    summary.lastSuccessfulArtifactVerificationAt,
                    formatDt,
                    formatLocale,
                    t,
                  )}
                </Typography.Text>
                <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                  {t('backupDr.recoverability.proofBlock.artifactVerificationScope')}
                </Typography.Paragraph>
              </Descriptions.Item>

              <Descriptions.Item label={t('backupDr.recoverability.proofBlock.restoreDrill')} span={2}>
                <Typography.Text strong>
                  {formatRecoverabilityTimestampOrProofGap(summary.lastSuccessfulRestoreProofAt, formatDt, formatLocale, t)}
                </Typography.Text>
                {summary.lastSuccessfulRestoreProofRunId ? (
                  <Typography.Text type="secondary" style={{ marginLeft: 8 }} copyable>
                    {summary.lastSuccessfulRestoreProofRunId}
                  </Typography.Text>
                ) : null}
                <div style={{ marginTop: 8 }}>
                  <Typography.Text type="secondary">{t('backupDr.recoverability.restoreProofAge')}: </Typography.Text>
                  <Tag color={freshnessTagColor(proofAgeFreshnessTier(summary.restoreProofAgeSeconds))}>
                    {formatAgeSeconds(summary.restoreProofAgeSeconds)}
                  </Tag>
                  <FreshnessFootnote tier={proofAgeFreshnessTier(summary.restoreProofAgeSeconds)} t={t} />
                </div>
              </Descriptions.Item>
            </Descriptions>

            <Divider style={{ margin: '12px 0' }} />

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
            </Descriptions>
          </div>

          <div
            style={{
              border: '1px solid #d9d9d9',
              borderRadius: 4,
              padding: 12,
              background: '#fafafa',
            }}
          >
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              {t('backupDr.recoverability.requestsSectionTitle')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
              {t('backupDr.recoverability.requestsSectionIntro')}
            </Typography.Paragraph>
            <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
              <Descriptions.Item label={t('backupDr.recoverability.latestBackupRequest')} span={2}>
                {formatDt(summary.latestRunAt, formatLocale)}{' '}
                {summary.latestRunStatus !== undefined && summary.latestRunStatus !== null ? (
                  <Tag color="default">{backupStatusLabel(summary.latestRunStatus, t)}</Tag>
                ) : (
                  '—'
                )}
              </Descriptions.Item>
              <Descriptions.Item label={t('backupDr.recoverability.latestRestoreRequest')} span={2}>
                {formatDt(summary.latestRestoreRunAt, formatLocale)}{' '}
                {summary.latestRestoreRunStatus !== undefined && summary.latestRestoreRunStatus !== null ? (
                  <Tag color="default">{restoreStatusLabel(summary.latestRestoreRunStatus, t)}</Tag>
                ) : (
                  '—'
                )}
              </Descriptions.Item>
            </Descriptions>
            <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
              {t('backupDr.recoverability.latestRequestVsProofHint')}
            </Typography.Paragraph>
          </div>
        </>
      )}
    </Card>
  );
}
