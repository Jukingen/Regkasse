'use client';

import { Col, Row, Statistic, Typography } from 'antd';
import dayjs from 'dayjs';
import React, { useMemo } from 'react';
import {
  Line,
  LineChart,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
  XAxis,
  YAxis,
} from 'recharts';

import { useGetApiReportsSales } from '@/api/generated/reports/reports';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserMonthDay } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

export function TodaySalesWidget({ title, dragHandleProps, onRefresh }: Props) {
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

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

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
            <ResponsiveContainer width="100%" height={120}>
              <LineChart data={chartData}>
                <XAxis dataKey="date" hide />
                <YAxis hide />
                <RechartsTooltip formatter={(v) => `€${Number(v ?? 0).toFixed(2)}`} />
                <Line type="monotone" dataKey="total" stroke="#1677ff" dot={false} />
              </LineChart>
            </ResponsiveContainer>
          ) : (
            <Typography.Text type="secondary">
              {t('dashboard.widgets.todaySales.noSalesToday')}
            </Typography.Text>
          )}
        </Col>
      </Row>
    </WidgetShell>
  );
}
