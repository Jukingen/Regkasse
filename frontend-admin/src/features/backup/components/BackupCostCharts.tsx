'use client';

import { Card, Col, Empty, Row, Typography } from 'antd';
import React from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { costSharePercents, type CostTrendPoint } from '@/features/backup/logic/backupDrInsights';
import { useI18n } from '@/i18n';

const TIER_COLORS = ['#1677ff', '#faad14', '#8c8c8c', '#52c41a'];

export type BackupCostTierSlice = {
  name: string;
  sizeGb: number;
  costEur?: number;
};

export type BackupCostChartsProps = {
  trend: CostTrendPoint[];
  tiers: BackupCostTierSlice[];
  retention: { actual: number; allHot: number; savingsPercent: number };
  monthlyEstimate: number;
};

function tierWeights(tiers: readonly BackupCostTierSlice[]): number[] {
  const costs = tiers.map((tier) => tier.costEur ?? 0);
  if (costs.some((cost) => cost > 0)) return costs;
  return tiers.map((tier) => tier.sizeGb);
}

export function BackupCostCharts({
  trend,
  tiers,
  retention,
  monthlyEstimate,
}: BackupCostChartsProps) {
  const { t } = useI18n();
  const shares = costSharePercents(tierWeights(tiers));
  const pieData = tiers
    .map((tier, index) => ({ name: tier.name, share: shares[index] ?? 0 }))
    .filter((tier) => tier.share > 0);
  const retentionBars = [
    { name: t('backupDr.costs.insights.retentionActual'), value: retention.actual },
    { name: t('backupDr.costs.insights.retentionAllHot'), value: retention.allHot },
  ];

  return (
    <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
      <Col xs={24} lg={14}>
        <Card size="small" title={t('backupDr.costs.insights.trendTitle')}>
          <Typography.Paragraph type="secondary">
            {t('backupDr.costs.insights.trendHint')}
          </Typography.Paragraph>
          {trend.every((point) => point.costEur === 0) ? (
            <Empty description={t('backupDr.costs.insights.emptyTrend')} />
          ) : (
            <div data-testid="backup-cost-trend" style={{ width: '100%', height: 240 }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={trend}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" hide />
                  <YAxis />
                  <Tooltip />
                  <Line
                    type="monotone"
                    dataKey="costEur"
                    stroke="#1677ff"
                    dot={false}
                    isAnimationActive={false}
                  />
                </LineChart>
              </ResponsiveContainer>
              <ul>
                {trend
                  .filter((point) => point.costEur > 0)
                  .slice(0, 8)
                  .map((point) => (
                    <li key={point.date}>
                      {point.date}: {point.costEur.toFixed(2)}
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </Card>
      </Col>
      <Col xs={24} lg={10}>
        <Card size="small" title={t('backupDr.costs.insights.tierTitle')}>
          <div data-testid="backup-cost-tiers" style={{ width: '100%', height: 220 }}>
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={pieData}
                  dataKey="share"
                  nameKey="name"
                  outerRadius={80}
                  isAnimationActive={false}
                  label={({ name, value }) => `${name ?? ''} ${Number(value)}%`}
                >
                  {pieData.map((tier) => {
                    const colorIndex = Math.max(0, tiers.findIndex((item) => item.name === tier.name));
                    return <Cell key={tier.name} fill={TIER_COLORS[colorIndex % TIER_COLORS.length]} />;
                  })}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          </div>
          {tiers.map((tier, index) => (
            <div key={tier.name}>
              <span>
                {tier.name}: {tier.sizeGb.toFixed(3)} GB
              </span>
              <span data-testid={`backup-cost-share-${tier.name}`}> {shares[index] ?? 0}%</span>
            </div>
          ))}
        </Card>
      </Col>
      <Col xs={24} lg={14}>
        <Card size="small" title={t('backupDr.costs.insights.retentionTitle')}>
          <div data-testid="backup-cost-retention" style={{ width: '100%', height: 220 }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={retentionBars}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="name" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="value" fill="#1677ff" isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>
      </Col>
      <Col xs={24} lg={10}>
        <Card size="small">
          <Typography.Text type="secondary">
            {t('backupDr.costs.insights.monthlyEstimate')}
          </Typography.Text>
          <Typography.Title level={3} style={{ marginTop: 8 }}>
            € {monthlyEstimate.toFixed(2)}
          </Typography.Title>
        </Card>
      </Col>
    </Row>
  );
}
