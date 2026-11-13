'use client';

/**
 * Visual progress for a backup run — polls run detail + optional auto-track of latest in-progress.
 */
import { Alert, Card, Progress, Space, Spin, Typography } from 'antd';
import React from 'react';

import { formatBackupDurationMs } from '@/features/backup-dr/logic/backupFormat';
import { useBackupProgress } from '@/features/backup/hooks/useBackupProgress';
import { useI18n } from '@/i18n';

export type BackupProgressProps = {
  /** Explicit run id; when omitted with autoTrackLatestInProgress, tracks active run. */
  backupRunId?: string | null;
  /** Default true when backupRunId is omitted. */
  autoTrackLatestInProgress?: boolean;
  /** Hide the card when there is nothing to show (default true). */
  hideWhenIdle?: boolean;
  size?: 'default' | 'small';
};

export function BackupProgress({
  backupRunId = null,
  autoTrackLatestInProgress,
  hideWhenIdle = true,
  size = 'small',
}: BackupProgressProps) {
  const { t } = useI18n();
  const autoTrack = autoTrackLatestInProgress ?? (backupRunId == null || backupRunId === '');

  const {
    data: progress,
    isLoading,
    isError,
  } = useBackupProgress(backupRunId, {
    autoTrackLatestInProgress: autoTrack,
  });

  if (isLoading) {
    return (
      <Card size={size} title={t('backupDr.progress.cardTitle')}>
        <Spin />
      </Card>
    );
  }

  if (isError) {
    return (
      <Card size={size} title={t('backupDr.progress.cardTitle')}>
        <Alert type="error" showIcon title={t('backupDr.errors.loadFailed')} />
      </Card>
    );
  }

  if (!progress) {
    if (hideWhenIdle) return null;
    return (
      <Card size={size} title={t('backupDr.progress.cardTitle')}>
        <Typography.Text type="secondary">{t('backupDr.progress.idle')}</Typography.Text>
      </Card>
    );
  }

  if (hideWhenIdle && !progress.isInProgress && !progress.isError) {
    return null;
  }

  const remainingLabel =
    progress.isInProgress && progress.estimatedRemainingMs != null
      ? t('backupDr.progress.estimatedRemaining', {
          time: formatBackupDurationMs(progress.estimatedRemainingMs, t),
        })
      : progress.isInProgress
        ? t('backupDr.progress.noEta')
        : null;

  return (
    <Card size={size} title={t('backupDr.progress.cardTitle')}>
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        <Typography.Text strong>{t(progress.statusTitleKey)}</Typography.Text>
        {progress.bodyKey ? (
          <Typography.Text type="secondary">{t(progress.bodyKey)}</Typography.Text>
        ) : null}

        <Progress
          percent={progress.percentage}
          status={progress.progressStatus}
          aria-label={t('backupDr.progress.cardTitle')}
        />

        {progress.totalSteps > 0 ? (
          <Typography.Text>
            {t('backupDr.progress.stepOf', {
              current: progress.currentStep,
              total: progress.totalSteps,
            })}
            {progress.currentStepTitleKey ? (
              <Typography.Text type="secondary">
                {' · '}
                {t(progress.currentStepTitleKey)}
              </Typography.Text>
            ) : null}
          </Typography.Text>
        ) : null}

        {remainingLabel ? (
          <Typography.Text type="secondary">
            {progress.estimatedRemainingMs != null ? (
              <>
                <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                  {t('backupDr.progress.etaDemotedLead')}
                </Typography.Text>
                {remainingLabel}
              </>
            ) : (
              remainingLabel
            )}
          </Typography.Text>
        ) : null}

        {progress.isError ? (
          <Alert type="error" showIcon title={t('backupDr.progress.errorAlert')} />
        ) : null}
      </Space>
    </Card>
  );
}
