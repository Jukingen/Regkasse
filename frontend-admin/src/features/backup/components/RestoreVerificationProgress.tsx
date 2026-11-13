'use client';

import { Alert, Card, Progress, Space, Spin, Steps, Typography } from 'antd';
import React from 'react';

import { useRestoreVerificationProgress } from '@/features/backup/hooks/useRestoreVerificationProgress';
import { restoreVerificationCheckLabelKey } from '@/features/backup/logic/restoreVerificationPresentation';
import { formatBackupDurationMs } from '@/features/backup-dr/logic/backupFormat';
import { useI18n } from '@/i18n';

export type RestoreVerificationProgressProps = {
  hideWhenIdle?: boolean;
  size?: 'default' | 'small';
};

export function RestoreVerificationProgress({
  hideWhenIdle = true,
  size = 'small',
}: RestoreVerificationProgressProps) {
  const { t } = useI18n();
  const { progress, isLoading, isError, isInProgress } = useRestoreVerificationProgress();

  if (isLoading) {
    return (
      <Card size={size} title={t('backupDr.restoreProgress.cardTitle')}>
        <Spin />
      </Card>
    );
  }

  if (isError) {
    return hideWhenIdle ? null : (
      <Card size={size} title={t('backupDr.restoreProgress.cardTitle')}>
        <Alert type="error" showIcon title={t('backupDr.restoreVerificationPage.loadError')} />
      </Card>
    );
  }

  if (!progress) {
    if (hideWhenIdle) return null;
    return (
      <Card size={size} title={t('backupDr.restoreProgress.cardTitle')}>
        <Typography.Text type="secondary">{t('backupDr.restoreProgress.idle')}</Typography.Text>
      </Card>
    );
  }

  if (hideWhenIdle && !isInProgress && !progress.isError) {
    return null;
  }

  const remainingLabel =
    progress.isInProgress && progress.estimatedRemainingMs != null
      ? t('backupDr.restoreProgress.estimatedRemaining', {
          time: formatBackupDurationMs(progress.estimatedRemainingMs, t),
        })
      : progress.isInProgress
        ? t('backupDr.restoreProgress.noEta')
        : null;

  return (
    <Card size={size} title={t('backupDr.restoreProgress.cardTitle')}>
      <Space orientation="vertical" size={10} style={{ width: '100%' }}>
        <Typography.Text strong>{t(progress.statusTitleKey)}</Typography.Text>
        <Progress
          percent={progress.percentage}
          status={progress.progressStatus}
          aria-label={t('backupDr.restoreProgress.cardTitle')}
        />
        <Steps
          size="small"
          current={Math.max(0, progress.currentStep - 1)}
          status={progress.isError ? 'error' : progress.isInProgress ? 'process' : 'finish'}
          items={progress.steps.map((step) => ({
            title: t(restoreVerificationCheckLabelKey(step.id)),
            status: step.state,
          }))}
        />
        {remainingLabel ? (
          <Typography.Text type="secondary">{remainingLabel}</Typography.Text>
        ) : null}
      </Space>
    </Card>
  );
}
