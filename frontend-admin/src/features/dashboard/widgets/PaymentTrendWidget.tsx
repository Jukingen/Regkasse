'use client';

import { ArrowDownOutlined, ArrowUpOutlined, MinusOutlined } from '@ant-design/icons';
import { Col, Row, Segmented, Skeleton, Statistic, Tag, Typography } from 'antd';
import dynamic from 'next/dynamic';
import React, { memo, useCallback, useMemo, useState } from 'react';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { parsePaymentTrendPeriod } from '@/features/dashboard/widgets/paymentTrendPeriod';
import { usePaymentTrends } from '@/features/payments/hooks/usePaymentTrends';
import type { TrendPeriod } from '@/features/payments/types/paymentTrends';
import { useI18n } from '@/i18n/I18nProvider';
import { formatUserMonthDay } from '@/lib/dateFormatter';

const PaymentTrendCharts = dynamic(() => import('./PaymentTrendCharts'), {
  ssr: false,
  loading: () => <Skeleton active paragraph={{ rows: 6 }} />,
});

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'> & {
  period?: TrendPeriod;
  onPeriodChange?: (period: TrendPeriod) => void;
};

const PERIOD_OPTIONS: TrendPeriod[] = ['Daily', 'Weekly', 'Monthly'];

export const PaymentTrendWidget = memo(function PaymentTrendWidget({
  title,
  dragHandleProps,
  onRefresh,
  period: periodProp,
  onPeriodChange,
}: Props) {
  const { t } = useI18n();
  const [localPeriod, setLocalPeriod] = useState<TrendPeriod>('Daily');
  const period = periodProp ?? localPeriod;

  const query = usePaymentTrends(period, null, true);

  const chartData = useMemo(
    () =>
      (query.data?.trendData ?? []).map((point) => ({
        label: point.label ?? formatUserMonthDay(point.date),
        revenue: point.totalAmount,
        count: point.transactionCount,
      })),
    [query.data?.trendData]
  );

  const comparison = query.data?.comparison;
  const summary = query.data?.summary;

  const handlePeriodChange = useCallback(
    (value: string | number) => {
      const next = parsePaymentTrendPeriod(value);
      setLocalPeriod(next);
      onPeriodChange?.(next);
    },
    [onPeriodChange]
  );

  const handleRefresh = useCallback(() => {
    void query.refetch();
    onRefresh?.();
  }, [query, onRefresh]);

  const trendTag = (() => {
    if (!comparison) return null;
    if (comparison.trend === 'up') {
      return (
        <Tag color="green" icon={<ArrowUpOutlined />}>
          {comparison.growthPercentage.toFixed(1)}%
        </Tag>
      );
    }
    if (comparison.trend === 'down') {
      return (
        <Tag color="red" icon={<ArrowDownOutlined />}>
          {comparison.growthPercentage.toFixed(1)}%
        </Tag>
      );
    }
    return <Tag icon={<MinusOutlined />}>{comparison.growthPercentage.toFixed(1)}%</Tag>;
  })();

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
      extra={
        <Segmented
          size="small"
          value={period}
          options={PERIOD_OPTIONS.map((p) => ({
            label: t(`payments.trends.period.${p}`),
            value: p,
          }))}
          onChange={handlePeriodChange}
        />
      }
    >
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Statistic
            title={t('payments.trends.totalRevenue')}
            value={summary?.totalRevenue ?? 0}
            precision={2}
            suffix="€"
            loading={query.isLoading}
          />
          <Typography.Text type="secondary">
            {t('payments.trends.transactions', {
              count: summary?.totalTransactions ?? 0,
            })}
          </Typography.Text>
        </Col>
        <Col xs={24} sm={8}>
          <Statistic
            title={t('payments.trends.avgTransaction')}
            value={summary?.averageTransactionValue ?? 0}
            precision={2}
            suffix="€"
            loading={query.isLoading}
          />
        </Col>
        <Col xs={24} sm={8}>
          <Statistic
            title={t('payments.trends.periodComparison')}
            value={comparison?.currentPeriodTotal ?? 0}
            precision={2}
            suffix="€"
            loading={query.isLoading}
          />
          {trendTag}
        </Col>
      </Row>

      <div style={{ marginTop: 16, minHeight: 220 }}>
        {chartData.length > 0 ? (
          <PaymentTrendCharts
            data={chartData}
            period={period}
            revenueLabel={t('payments.trends.chart.revenue')}
            countLabel={t('payments.trends.chart.count')}
          />
        ) : (
          <Typography.Text type="secondary">{t('payments.trends.empty')}</Typography.Text>
        )}
      </div>

      {summary?.mostUsedPaymentMethod ? (
        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
          {t('payments.trends.insights', {
            method: summary.mostUsedPaymentMethod,
            hour: summary.peakHour,
            bestDay: summary.bestDay ?? '—',
          })}
        </Typography.Text>
      ) : null}
    </WidgetShell>
  );
});

export { parsePaymentTrendPeriod };
