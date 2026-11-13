'use client';

import { Alert, Button, Skeleton } from 'antd';
import Link from 'next/link';
import React from 'react';
import { useQuery } from '@tanstack/react-query';

import { getFiskalyStatus } from '@/features/dashboard/api/fiskalyStatus';
import { useI18n } from '@/i18n/I18nProvider';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

type IndicatorKind = 'success' | 'warning' | 'error';

export function TseStatusIndicator() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canOpenSettings =
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL) || hasPermission(AppPermissions.CashRegisterManage);

  const query = useQuery({
    queryKey: ['admin', 'fiskaly', 'status', 'indicator'],
    queryFn: ({ signal }) => getFiskalyStatus(true, signal),
    staleTime: 15_000,
  });

  if (query.isLoading) {
    return <Skeleton active paragraph={{ rows: 2 }} />;
  }

  const data = query.data;
  const status = resolveIndicator(data, t);

  return (
    <Alert
      type={status.kind}
      showIcon
      title={status.title}
      description={status.description}
      action={
        status.kind !== 'success' && canOpenSettings ? (
          <Link href="/admin/tse/fiskaly">
            <Button size="small">{t('tseFiskaly.indicator.configure')}</Button>
          </Link>
        ) : undefined
      }
    />
  );
}

function resolveIndicator(
  data: Awaited<ReturnType<typeof getFiskalyStatus>> | undefined,
  t: (key: string, values?: Record<string, string | number>) => string
): { kind: IndicatorKind; title: string; description: string } {
  if (!data?.isEnabled) {
    return {
      kind: 'error',
      title: t('tseFiskaly.indicator.disabledTitle'),
      description: t('tseFiskaly.indicator.disabledDescription'),
    };
  }

  if (!data.isConfigured) {
    return {
      kind: 'error',
      title: t('tseFiskaly.indicator.notConfiguredTitle'),
      description: t('tseFiskaly.indicator.notConfiguredDescription'),
    };
  }

  if (!data.isAuthenticated) {
    return {
      kind: 'warning',
      title: t('tseFiskaly.indicator.authFailedTitle'),
      description: data.error?.trim() || t('tseFiskaly.indicator.authFailedDescription'),
    };
  }

  if (!data.scuInitialized) {
    return {
      kind: 'warning',
      title: t('tseFiskaly.indicator.scuNotInitializedTitle'),
      description: t('tseFiskaly.indicator.scuNotInitializedDescription'),
    };
  }

  return {
    kind: 'success',
    title: t('tseFiskaly.indicator.activeTitle'),
    description: t('tseFiskaly.indicator.activeDescription', {
      scu: data.scuId?.trim() || '—',
      env: data.environment,
    }),
  };
}
