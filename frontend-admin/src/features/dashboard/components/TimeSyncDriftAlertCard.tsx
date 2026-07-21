'use client';

import { ClockCircleOutlined, LinkOutlined } from '@ant-design/icons';
import { Alert } from 'antd';
import NextLink from 'next/link';
import React from 'react';

import { getTimeSyncDriftSummary } from '@/api/manual/adminSystemTimeSync';
import { useAuthorizationGate, useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';

const REFETCH_MS = 60_000;

/**
 * Dashboard alert when mirrored cash-register drift exceeds effective max offset (server-side NTP check).
 */
export function TimeSyncDriftAlertCard() {
  const { t } = useI18n();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.SETTINGS_MANAGE,
  });

  const driftQuery = useAuthorizedQuery({
    queryKey: ['admin', 'time-sync', 'drift-summary'],
    queryFn: () => getTimeSyncDriftSummary(),
    requiredPermission: PERMISSIONS.SETTINGS_MANAGE,
    refetchInterval: REFETCH_MS,
  });

  if (!isAuthorized) return null;

  if (!driftQuery.data?.hasActiveDrift) return null;

  const max =
    driftQuery.data.largestAbsoluteOffsetSeconds != null
      ? driftQuery.data.largestAbsoluteOffsetSeconds.toFixed(2)
      : '—';

  return (
    <Alert
      type="warning"
      showIcon
      icon={<ClockCircleOutlined />}
      style={{ marginBottom: 24 }}
      title={t('timeSync.dashboard.alertTitle')}
      description={
        <>
          {t('timeSync.dashboard.alertDescription', {
            count: driftQuery.data.registerCountOverThreshold,
            threshold: driftQuery.data.maxAllowedOffsetSecondsThreshold,
            max,
          })}{' '}
          <NextLink href="/admin/system/time-sync">
            <LinkOutlined /> {t('timeSync.dashboard.link')}
          </NextLink>
        </>
      }
    />
  );
}
