'use client';

import { Card, Col, Row, Skeleton, Statistic, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dynamic from 'next/dynamic';
import React, { useMemo } from 'react';

import { getPaymentVolumeAnalytics, platformAnalyticsQueryKeys } from '@/features/dashboard/api/platformAnalytics';
import { useI18n } from '@/i18n';

const PaymentVolumeChart = dynamic(
  () => import('@/features/dashboard/components/PaymentVolumeChart'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> }
);

/** Super Admin POS payment volume (fiscal GMV — not license MRR). */
export function PaymentVolumeAnalyticsWidget() {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: platformAnalyticsQueryKeys.paymentVolume(),
    queryFn: () => getPaymentVolumeAnalytics(undefined, undefined, 'month'),
    staleTime: 60_000,
  });

  const chartData = useMemo(
    () =>
      (query.data?.monthlyVolume ?? []).map((row) => ({
        label: row.yearMonth,
        revenue: row.revenue,
      })),
    [query.data?.monthlyVolume]
  );

  if (query.isLoading) {
    return (
      <Card title={t('dashboard.platformAnalytics.volumeTitle')} style={{ marginBottom: 16 }}>
        <Skeleton active paragraph={{ rows: 3 }} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return (
      <Card title={t('dashboard.platformAnalytics.volumeTitle')} style={{ marginBottom: 16 }}>
        <Typography.Text type="danger">{t('dashboard.platformAnalytics.loadFailed')}</Typography.Text>
      </Card>
    );
  }

  const d = query.data;
  const growth = d.monthlyGrowth;
  return (
    <Card title={t('dashboard.platformAnalytics.volumeTitle')} style={{ marginBottom: 16 }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} sm={8}>
          <Statistic
            title={t('dashboard.platformAnalytics.revenueThisMonth')}
            value={d.revenueThisMonth}
            precision={2}
            suffix="€"
          />
        </Col>
        <Col xs={12} sm={8}>
          <Statistic
            title={t('dashboard.platformAnalytics.monthlyGrowth')}
            value={growth}
            precision={2}
            suffix="%"
            styles={{ content: { color: growth > 0 ? '#16a34a' : growth < 0 ? '#dc2626' : undefined } }}
          />
        </Col>
        <Col xs={12} sm={8}>
          <Statistic
            title={t('dashboard.platformAnalytics.averageTicket')}
            value={d.averageTransactionValue}
            precision={2}
            suffix="€"
          />
        </Col>
      </Row>
      <PaymentVolumeChart data={chartData} revenueLabel={t('dashboard.platformAnalytics.revenue')} />
    </Card>
  );
}
