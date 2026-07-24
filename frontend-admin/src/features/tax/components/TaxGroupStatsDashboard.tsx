'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Col, Progress, Row, Skeleton, Space, Statistic, Typography } from 'antd';
import React, { useMemo } from 'react';

import {
  getTaxGroupStats,
  taxGroupStatsQueryKey,
  type TaxGroupStat,
} from '@/features/tax/api/taxGroups';
import { formatCurrency, useI18n } from '@/i18n';

function currentYearUtcRange(): { fromUtc: string; toUtc: string } {
  const now = new Date();
  const from = new Date(Date.UTC(now.getUTCFullYear(), 0, 1, 0, 0, 0));
  const to = new Date(now.getTime() + 24 * 60 * 60 * 1000);
  return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
}

function StatCard({ group, productsSuffix }: { group: TaxGroupStat; productsSuffix: string }) {
  const { t, formatLocale } = useI18n();
  const stroke = group.color || undefined;

  return (
    <Card size="small">
      <Statistic
        title={
          <Space size={6} wrap>
            {group.icon ? <span aria-hidden>{group.icon}</span> : null}
            <span>{group.name}</span>
            <Typography.Text type="secondary" style={{ fontWeight: 400 }}>
              ({group.rate}%)
            </Typography.Text>
          </Space>
        }
        value={group.productCount}
        suffix={productsSuffix}
      />
      <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 13 }}>
        {t('settings.taxGroups.stats.revenue', {
          amount: formatCurrency(group.revenue, formatLocale, { currency: 'EUR' }),
        })}
      </Typography.Text>
      <Progress
        percent={Number(group.percentage) || 0}
        strokeColor={stroke}
        size="small"
        style={{ marginTop: 8, marginBottom: 0 }}
        format={(pct) => `${pct ?? 0}%`}
      />
    </Card>
  );
}

export function TaxGroupStatsDashboard() {
  const { t, formatLocale } = useI18n();
  const range = useMemo(() => currentYearUtcRange(), []);

  const { data, isLoading, isError } = useQuery({
    queryKey: [...taxGroupStatsQueryKey, range.fromUtc, range.toUtc],
    queryFn: () => getTaxGroupStats(range),
    staleTime: 60_000,
  });

  if (isLoading) {
    return (
      <Card title={t('settings.taxGroups.stats.cardTitle')}>
        <Row gutter={[16, 16]}>
          {Array.from({ length: 4 }).map((_, i) => (
            <Col xs={24} sm={12} lg={6} key={i}>
              <Card size="small">
                <Skeleton active paragraph={{ rows: 2 }} />
              </Card>
            </Col>
          ))}
        </Row>
      </Card>
    );
  }

  if (isError) {
    return (
      <Card title={t('settings.taxGroups.stats.cardTitle')}>
        <Alert type="error" showIcon title={t('settings.taxGroups.stats.loadFailed')} />
      </Card>
    );
  }

  const groups = data?.groups ?? [];

  return (
    <Card
      title={t('settings.taxGroups.stats.cardTitle')}
      extra={
        data ? (
          <Typography.Text type="secondary">
            {t('settings.taxGroups.stats.summary', {
              products: data.totalProducts,
              revenue: formatCurrency(data.totalRevenue, formatLocale, { currency: 'EUR' }),
            })}
          </Typography.Text>
        ) : null
      }
    >
      {groups.length === 0 ? (
        <Alert type="info" showIcon title={t('settings.taxGroups.stats.empty')} />
      ) : (
        <Row gutter={[16, 16]}>
          {groups.map((group) => (
            <Col xs={24} sm={12} lg={6} key={group.id}>
              <StatCard group={group} productsSuffix={t('settings.taxGroups.stats.productsSuffix')} />
            </Col>
          ))}
        </Row>
      )}
    </Card>
  );
}
