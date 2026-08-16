'use client';

import { Alert, Badge, Button, Skeleton, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import React from 'react';

import { getFiskalyStatus } from '@/features/dashboard/api/fiskalyStatus';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { AppPermissions } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps'>;

export function FiskalyStatusWidget({ title, dragHandleProps }: Props) {
  const { t } = useI18n();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: AppPermissions.CashRegisterView,
  });
  const query = useQuery({
    queryKey: ['admin', 'fiskaly', 'status'],
    queryFn: ({ signal }) => getFiskalyStatus(true, signal),
    enabled: isAuthorized,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
  });

  if (!isAuthorized) return null;

  const data = query.data;
  let badge = <Badge status="default" text={t('dashboard.fiskalyStatus.disabled')} />;
  if (data?.isEnabled && !data.isConfigured) {
    badge = <Badge status="error" text={t('dashboard.fiskalyStatus.not_configured')} />;
  } else if (data?.isEnabled && !data.isAuthenticated) {
    badge = <Badge status="warning" text={t('dashboard.fiskalyStatus.auth_failed')} />;
  } else if (data?.isEnabled && data.isAuthenticated) {
    badge = <Badge status="success" text={t('dashboard.fiskalyStatus.active')} />;
  }

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={() => {
        void query.refetch();
      }}
      refreshing={query.isFetching}
    >
      {query.isLoading ? (
        <Skeleton active paragraph={{ rows: 3 }} />
      ) : data ? (
        <>
          {badge}
          {data.isEnabled && data.isAuthenticated ? (
            <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
              {t('dashboard.fiskalyStatus.environment', { env: data.environment })}
              {data.lastCheck
                ? ` · ${t('dashboard.fiskalyStatus.last_check', {
                    time: formatDateTime(data.lastCheck, ''),
                  })}`
                : null}
            </Typography.Paragraph>
          ) : null}
          {data.error ? (
            <Alert type="error" showIcon title={data.error} style={{ marginTop: 12 }} />
          ) : null}
          <Button size="small" style={{ marginTop: 12 }} onClick={() => void query.refetch()}>
            {t('dashboard.fiskalyStatus.refresh')}
          </Button>
        </>
      ) : null}
    </WidgetShell>
  );
}
