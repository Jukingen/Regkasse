'use client';

import { Card, Col, Row, Skeleton, Statistic, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import React from 'react';

import { getCustomerAnalytics } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n';

const QUERY_KEY = ['admin', 'analytics', 'customers'] as const;

/** Super Admin KPI strip for customer / mandant growth. */
export function CustomerAnalyticsCards() {
  const { t } = useI18n();
  const query = useQuery({
    queryKey: QUERY_KEY,
    queryFn: getCustomerAnalytics,
    staleTime: 60_000,
  });

  if (query.isLoading) {
    return (
      <Card title={t('tenants.analytics.title')} style={{ marginBottom: 16 }}>
        <Skeleton active paragraph={{ rows: 1 }} />
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
          <Statistic title={t('tenants.analytics.expiringSoon')} value={d.expiringSoon} />
        </Col>
        <Col xs={12} sm={6}>
          <Statistic
            title={t('tenants.analytics.mrr')}
            value={d.mrr}
            precision={2}
            suffix="€"
          />
        </Col>
      </Row>
    </Card>
  );
}
