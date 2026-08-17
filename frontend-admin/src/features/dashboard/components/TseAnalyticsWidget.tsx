'use client';

import { Card, Col, Row, Skeleton, Statistic, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dynamic from 'next/dynamic';
import React, { useMemo } from 'react';

import { getTseAnalytics, platformAnalyticsQueryKeys } from '@/features/dashboard/api/platformAnalytics';
import { useI18n } from '@/i18n';

const TseUsageChart = dynamic(() => import('@/features/dashboard/components/TseUsageChart'), {
  ssr: false,
  loading: () => <Skeleton active paragraph={{ rows: 4 }} />,
});

/** Super Admin fleet TSE usage (diagnostic — not DEP evidence). */
export function TseAnalyticsWidget() {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: platformAnalyticsQueryKeys.tse(),
    queryFn: () => getTseAnalytics(),
    staleTime: 60_000,
  });

  const chartData = useMemo(
    () =>
      (query.data?.dailyUsage ?? []).map((row) => ({
        label: typeof row.date === 'string' ? row.date.slice(0, 10) : String(row.date),
        signatures: row.signatures,
      })),
    [query.data?.dailyUsage]
  );

  if (query.isLoading) {
    return (
      <Card title={t('dashboard.platformAnalytics.tseTitle')} style={{ marginBottom: 16 }}>
        <Skeleton active paragraph={{ rows: 3 }} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return (
      <Card title={t('dashboard.platformAnalytics.tseTitle')} style={{ marginBottom: 16 }}>
        <Typography.Text type="danger">{t('dashboard.platformAnalytics.loadFailed')}</Typography.Text>
      </Card>
    );
  }

  const d = query.data;
  return (
    <Card title={t('dashboard.platformAnalytics.tseTitle')} style={{ marginBottom: 16 }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} sm={8}>
          <Statistic title={t('dashboard.platformAnalytics.activeRegisters')} value={d.activeRegisters} />
        </Col>
        <Col xs={12} sm={8}>
          <Statistic title={t('dashboard.platformAnalytics.signaturesToday')} value={d.signaturesToday} />
        </Col>
        <Col xs={12} sm={8}>
          <Statistic
            title={t('dashboard.platformAnalytics.failedSignatures')}
            value={d.failedSignatures}
            styles={{ content: { color: d.failedSignatures > 0 ? '#dc2626' : undefined } }}
          />
        </Col>
      </Row>
      <TseUsageChart data={chartData} signaturesLabel={t('dashboard.platformAnalytics.signatures')} />
    </Card>
  );
}
