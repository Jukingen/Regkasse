'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Col, Row, Statistic, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDateTime } from '@/i18n/formatting';
import { translateApiError } from '@/lib/api/errorTranslator';
import { customInstance } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

type OutboxSummary = {
  pendingCount: number;
  failedLast24Hours: number;
  lastSuccessfulSubmissionUtc: string | null;
};

const SUMMARY_KEY = ['finanzonline-outbox-summary'] as const;

export function FinanzOnlineOutboxWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, formatLocale } = useI18n();
  const { user } = useAuth();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.FINANZONLINE_VIEW,
  });
  const superAdmin = isSuperAdmin(user?.role);

  const summaryQuery = useQuery({
    queryKey: SUMMARY_KEY,
    enabled: isAuthorized,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    queryFn: () =>
      customInstance<OutboxSummary>({
        url: '/api/admin/finanzonline-outbox/summary',
        method: 'GET',
      }),
  });

  const retryAll = useMutation({
    mutationFn: () =>
      customInstance<{ retriedCount: number }>({
        url: '/api/admin/finanzonline-outbox/retry-all-failed',
        method: 'POST',
      }),
    onSuccess: (result) => {
      notify.success(t('dashboard.finanzOnlineOutbox.retryQueued', { count: result.retriedCount }));
      void queryClient.invalidateQueries({ queryKey: SUMMARY_KEY });
    },
    onError: (error: unknown) => {
      notify.error(
        translateApiError(t, error, {
          logContext: 'FinanzOnlineOutboxWidget.retryAll',
          fallbackKey: 'dashboard.finanzOnlineOutbox.retryError',
        })
      );
    },
  });

  const pending = summaryQuery.data?.pendingCount ?? 0;
  const failed = summaryQuery.data?.failedLast24Hours ?? 0;
  const lastSuccess = summaryQuery.data?.lastSuccessfulSubmissionUtc
    ? formatDateTime(summaryQuery.data.lastSuccessfulSubmissionUtc, formatLocale, {
        hour: '2-digit',
        minute: '2-digit',
      })
    : t('dashboard.finanzOnlineOutbox.lastSuccessNever');

  const confirmRetry = () => {
    modal.confirm({
      title: t('dashboard.finanzOnlineOutbox.retryConfirmTitle'),
      content: t('dashboard.finanzOnlineOutbox.retryConfirmBody'),
      okText: t('dashboard.finanzOnlineOutbox.retryAllFailed'),
      onOk: () => retryAll.mutateAsync(),
    });
  };

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={() => {
        void summaryQuery.refetch();
        onRefresh?.();
      }}
      refreshing={summaryQuery.isFetching}
    >
      <Row gutter={16}>
        <Col span={12}>
          <Statistic
            title={t('dashboard.finanzOnlineOutbox.pending')}
            value={pending}
            loading={summaryQuery.isLoading}
          />
        </Col>
        <Col span={12}>
          <Statistic
            title={t('dashboard.finanzOnlineOutbox.failed24h')}
            value={failed}
            loading={summaryQuery.isLoading}
            styles={failed > 0 ? { content: { color: '#cf1322' } } : undefined}
          />
        </Col>
      </Row>
      <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 8 }}>
        {t('dashboard.finanzOnlineOutbox.lastSuccess')}: {lastSuccess}
      </Typography.Paragraph>
      {superAdmin ? (
        <Button
          danger
          data-testid="fon-outbox-retry-all"
          loading={retryAll.isPending}
          onClick={confirmRetry}
          style={{ marginBottom: 8 }}
        >
          {t('dashboard.finanzOnlineOutbox.retryAllFailed')}
        </Button>
      ) : null}
      <div>
        <Link href="/rksv/finanz-online-outbox">{t('dashboard.finanzOnlineOutbox.openQueue')}</Link>
      </div>
    </WidgetShell>
  );
}
