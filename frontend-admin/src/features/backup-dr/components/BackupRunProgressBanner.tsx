'use client';

/**
 * Son yedek çalıştırmasının sürüyor / bitti durumunu üst düzey uyarı ile gösterir; ortalama süreden tahmini bitiş (varsa).
 */

import React, { useMemo } from 'react';
import { Alert, Space, Typography } from 'antd';
import type { BackupRunResponseDto } from '@/api/generated/model';

export interface BackupRunProgressBannerProps {
  latest: BackupRunResponseDto | undefined | null;
  /** Latest run detail: when true, terminal success is Fake/Stub — must not use production-success styling. */
  isSimulatedExecution?: boolean;
  averageSucceededDurationSeconds: number | undefined | null;
  averageSucceededDurationSampleCount: number | undefined | null;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function buildEtaDescription(
  status: number,
  requestedAt: string | undefined,
  startedAt: string | undefined,
  avgSec: number | undefined,
  sampleCount: number | undefined,
  formatDt: (iso: string | undefined | null, locale: string) => string,
  formatLocale: string,
  t: (key: string, options?: Record<string, string | number>) => string,
): string | undefined {
  if (avgSec == null || sampleCount == null || sampleCount < 1) {
    return t('backupDr.progress.noEta');
  }
  const n = String(sampleCount);
  if ((status === 1 || status === 2) && startedAt) {
    const end = new Date(new Date(startedAt).getTime() + avgSec * 1000).toISOString();
    return t('backupDr.progress.etaFromStart', {
      n,
      time: formatDt(end, formatLocale),
    });
  }
  if (status === 0 && requestedAt) {
    const end = new Date(new Date(requestedAt).getTime() + avgSec * 1000).toISOString();
    return t('backupDr.progress.etaQueued', {
      n,
      time: formatDt(end, formatLocale),
    });
  }
  return t('backupDr.progress.noEta');
}

export function BackupRunProgressBanner({
  latest,
  isSimulatedExecution = false,
  averageSucceededDurationSeconds,
  averageSucceededDurationSampleCount,
  formatDt,
  formatLocale,
  t,
}: BackupRunProgressBannerProps) {
  const s = latest?.status;
  const body = useMemo(() => {
    if (s === undefined || s === null) return null;
    if (s === 0) return t('backupDr.progress.bodyQueued');
    if (s === 1) return t('backupDr.progress.bodyRunning');
    if (s === 2) return t('backupDr.progress.bodyAwaiting');
    return null;
  }, [s, t]);

  const title = useMemo(() => {
    if (s === undefined || s === null) return null;
    if (s === 0) return t('backupDr.progress.titleQueued');
    if (s === 1) return t('backupDr.progress.titleRunning');
    if (s === 2) return t('backupDr.progress.titleAwaiting');
    if (s === 3)
      return isSimulatedExecution ? t('backupDr.progress.finishedSimulatedOk') : t('backupDr.progress.finishedOk');
    if (s === 4) return t('backupDr.progress.finishedFailed');
    if (s === 5) return t('backupDr.progress.finishedVerificationFailed');
    if (s === 6) return t('backupDr.progress.finishedCancelled');
    return null;
  }, [s, isSimulatedExecution, t]);

  const etaLine = useMemo(() => {
    if (s === undefined || s === null) return undefined;
    if (s !== 0 && s !== 1 && s !== 2) return undefined;
    return buildEtaDescription(
      s,
      latest?.requestedAt,
      latest?.startedAt,
      averageSucceededDurationSeconds ?? undefined,
      averageSucceededDurationSampleCount ?? undefined,
      formatDt,
      formatLocale,
      t,
    );
  }, [
    s,
    latest?.requestedAt,
    latest?.startedAt,
    averageSucceededDurationSeconds,
    averageSucceededDurationSampleCount,
    formatDt,
    formatLocale,
    t,
  ]);

  if (!latest || title == null) return null;

  if (s === 0 || s === 1 || s === 2) {
    return (
      <Alert
        type="warning"
        showIcon
        message={title}
        description={
          <Space direction="vertical" size={4} style={{ width: '100%' }}>
            {body ? <Typography.Text>{body}</Typography.Text> : null}
            {etaLine ? <Typography.Text type="secondary">{etaLine}</Typography.Text> : null}
          </Space>
        }
        style={{ marginBottom: 12 }}
      />
    );
  }

  const alertType =
    s === 3 ? (isSimulatedExecution ? 'warning' : 'success') : s === 6 ? 'info' : 'error';
  const description =
    s === 3 && isSimulatedExecution ? (
      <Typography.Text type="secondary">{t('backupDr.progress.finishedSimulatedOkDetail')}</Typography.Text>
    ) : undefined;
  return (
    <Alert
      type={alertType}
      showIcon
      message={title}
      description={description}
      style={{ marginBottom: 12 }}
    />
  );
}
