'use client';

import { Card, Col, Row, Skeleton, Statistic, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dynamic from 'next/dynamic';
import React, { useMemo } from 'react';

import { getCustomerAnalytics, platformAnalyticsQueryKeys } from '@/features/dashboard/api/platformAnalytics';
import { useI18n } from '@/i18n';

const PlanDistributionChart = dynamic(
  () => import('@/features/dashboard/components/PlanDistributionChart'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> }
);

/** Super Admin KPI strip for mandant health, revenue, and plan mix. */
export function CustomerAnalyticsCards() {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: platformAnalyticsQueryKeys.customers,
    queryFn: getCustomerAnalytics,
    staleTime: 60_000,
  });

  const planSlices = useMemo(() => {
    const p = query.data?.planDistribution;
    if (!p) return [];
    return [
      { name: t('tenants.analytics.planTrial'), value: p.trial },
      { name: t('tenants.analytics.planStarter'), value: p.starter },
      { name: t('tenants.analytics.planBusiness'), value: p.business },
      { name: t('tenants.analytics.planPlus'), value: p.plus },
    ];
  }, [query.data?.planDistribution, t]);

  if (query.isLoading) {
    return (
      <Card title={t('tenants.analytics.title')} style={{ marginBottom: 16 }}>
        <Skeleton active paragraph={{ rows: 3 }} />
      </Card>
    );
  }

  if (query.isError || !query.data) {
    return (
      <Card title={t('tenants.analytics.title')} style={{ marginBottom: 16 }}>
        <Typography.Text type="danger">{t('tenants.analytics.loadFailed')}</Typography.Text>
      </Card>
    );
  }

  const d = query.data;
  const ltv = d.customerLtv;

  return (
    <Card title={t('tenants.analytics.title')} style={{ marginBottom: 16 }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} sm={6}>
          <Statistic title={t('tenants.analytics.totalTenants')} value={d.totalTenants} />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic title={t('tenants.analytics.activeTenants')} value={d.activeTenants} />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('tenants.analytics.churnRate')}
            value={d.churnRate}
            precision={2}
            suffix="%"
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('tenants.analytics.arpu')}
            value={d.arpu}
            precision={2}
            suffix="€"
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic title={t('tenants.analytics.paidTenants')} value={d.paidTenants} />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic title={t('tenants.analytics.trialTenants')} value={d.trialTenants} />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('tenants.analytics.mrr')}
            value={d.mrr}
            precision={2}
            suffix="€"
          />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('tenants.analytics.ltv')}
            value={ltv ?? 0}
            precision={2}
            suffix="€"
            formatter={ltv == null ? () => '—' : undefined}
          />
        </Col>
      </Row>
      <Typography.Text type="secondary" style={{ display: 'block', marginTop: 16, marginBottom: 8 }}>
        {t('tenants.analytics.planDistribution')}
      </Typography.Text>
      <PlanDistributionChart data={planSlices} />
    </Card>
  );
}
