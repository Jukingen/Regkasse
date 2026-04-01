'use client';

/**
 * Son restore doğrulama (drill) özeti — yedek pipeline’ından ayrı blok.
 */

import React from 'react';
import { Alert, Card, Descriptions, Tag, Typography } from 'antd';
import type { RestoreVerificationRunResponseDto } from '@/api/generated/model';

export interface RestoreVerificationCardProps {
  run: RestoreVerificationRunResponseDto | undefined | null;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  restoreStatusTagColor: (status: number) => string;
  restoreStatusLabel: (status: number | undefined, t: (k: string) => string) => string;
  dumpInspectionTriState: (rr: RestoreVerificationRunResponseDto | undefined | null) => boolean | undefined;
  t: (k: string) => string;
}

export function RestoreVerificationCard({
  run,
  formatDt,
  formatLocale,
  restoreStatusTagColor,
  restoreStatusLabel,
  dumpInspectionTriState,
  t,
}: RestoreVerificationCardProps) {
  return (
    <Card title={t('backupDr.restoreVerification.title')} size="small">
      <Typography.Paragraph type="secondary">{t('backupDr.restoreVerification.explanation')}</Typography.Paragraph>
      {!run ? (
        <Typography.Text type="secondary">{t('backupDr.restoreVerification.none')}</Typography.Text>
      ) : (
        <>
          {run.status === 3 ? (
            <Alert
              type="error"
              showIcon
              style={{ marginBottom: 12 }}
              message={t('backupDr.restoreVerification.drillFailedProminent')}
              description={
                <Typography.Text type="danger" style={{ whiteSpace: 'pre-wrap' }}>
                  {[run.failureCode, (run.failureDetail ?? '').trim()].filter(Boolean).join(' — ') || '—'}
                </Typography.Text>
              }
            />
          ) : null}
          <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={t('backupDr.table.status')}>
            <Tag color={restoreStatusTagColor(run.status ?? -1)}>{restoreStatusLabel(run.status, t)}</Tag>
          </Descriptions.Item>
          {run.failureCode || run.failureDetail ? (
            <>
              <Descriptions.Item label={t('backupDr.restoreVerification.failureCode')}>
                {run.failureCode ?? '—'}
              </Descriptions.Item>
              <Descriptions.Item label={t('backupDr.restoreVerification.failureDetail')} span={1}>
                <Typography.Text type="danger" style={{ whiteSpace: 'pre-wrap' }}>
                  {(run.failureDetail ?? '').trim() || '—'}
                </Typography.Text>
              </Descriptions.Item>
            </>
          ) : null}
          <Descriptions.Item label={t('backupDr.restoreVerification.block.dumpInspection')}>
            {dumpInspectionTriState(run) === undefined
              ? '—'
              : dumpInspectionTriState(run)
                ? t('backupDr.triState.ok')
                : t('backupDr.triState.fail')}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.restoreVerification.block.restoreAttempt')}>
            {!run.restoreAttemptExecuted
              ? t('backupDr.restoreAttempt.notRun')
              : run.restoreAttemptPassed === true
                ? t('backupDr.triState.ok')
                : run.restoreAttemptPassed === false
                  ? t('backupDr.triState.fail')
                  : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.restoreVerification.fiscalSql')}>
            {run.fiscalSqlSkipped
              ? `${t('backupDr.restoreVerification.skipped')} (${run.fiscalSqlSkipReason ?? '—'})`
              : run.fiscalSqlPassed === true
                ? t('backupDr.triState.ok')
                : run.fiscalSqlPassed === false
                  ? t('backupDr.triState.fail')
                  : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.restoreVerification.integrity')}>
            {run.integrityChecksPassed === undefined
              ? '—'
              : `${run.integrityChecksPassed ? t('backupDr.triState.ok') : t('backupDr.triState.issues')} (${run.integrityScope ?? '—'})`}
          </Descriptions.Item>
          <Descriptions.Item label={t('backupDr.latestRun.completed')}>
            {formatDt(run.completedAt, formatLocale)}
          </Descriptions.Item>
        </Descriptions>
        </>
      )}
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        {t('backupDr.restoreVerification.strongerThanArtifact')}
      </Typography.Paragraph>
    </Card>
  );
}
