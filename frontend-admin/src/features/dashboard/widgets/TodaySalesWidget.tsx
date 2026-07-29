'use client';

import { Col, Row, Skeleton, Statistic, Typography } from 'antd';
import dayjs from 'dayjs';
import dynamic from 'next/dynamic';
import React, { memo, useCallback, useMemo } from 'react';

import { useGetApiReportsSales } from '@/api/generated/reports/reports';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserMonthDay } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';

const TodaySalesChart = dynamic(() => import('./TodaySalesChart'), {
  ssr: false,
  loading: () => <Skeleton active paragraph={{ rows: 6 }} />,
});

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export const TodaySalesWidget = memo(function TodaySalesWidget({
  title,
  dragHandleProps,
  onRefresh,
}: Props) {
  const { t } = useI18n();
  const today = dayjs().format('YYYY-MM-DD');
  const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.REPORT_VIEW });
  const query = useGetApiReportsSales(
    { startDate: today, endDate: today },
    {
      query: {
        enabled: isAuthorized,
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
      },
    }
  );

  const chartData = useMemo(
    () =>
      (query.data?.dailySales ?? []).map((d) => ({
        date: d.date ? formatUserMonthDay(d.date) || '—' : '—',
        total: d.total ?? 0,
      })),
    [query.data?.dailySales]
  );

  const handleRefresh = useCallback(() => {
    void query.refetch();
    onRefresh?.();
  }, [query, onRefresh]);

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
    >
      <Row gutter={16}>
        <Col xs={24} sm={12}>
          <Statistic
            title={t('dashboard.widgets.todaySales.totalToday')}
            value={query.data?.totalSales ?? 0}
            precision={2}
            suffix="€"
            loading={query.isLoading}
          />
          <Typography.Text type="secondary">
            {t('dashboard.widgets.todaySales.salesCount', {
              count: query.data?.totalInvoices ?? 0,
            })}
          </Typography.Text>
        </Col>
        <Col xs={24} sm={12} style={{ minHeight: 120 }}>
          {chartData.length > 0 ? (
            <TodaySalesChart data={chartData} />
          ) : (
            <Typography.Text type="secondary">
              {t('dashboard.widgets.todaySales.noSalesToday')}
            </Typography.Text>
          )}
        </Col>
      </Row>
    </WidgetShell>
  );
});
