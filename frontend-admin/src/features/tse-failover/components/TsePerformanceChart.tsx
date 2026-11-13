'use client';

import { Empty, Spin, Typography } from 'antd';
import React, { useMemo } from 'react';
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { useI18n } from '@/i18n/I18nProvider';
import { formatUtcDateTime } from '@/lib/dateUtils';

import type { TsePerformancePoint } from '../types';

type Props = {
  data: TsePerformancePoint[] | undefined;
  loading?: boolean;
  slowThresholdMs?: number;
  criticalThresholdMs?: number;
  height?: number;
};

type ChartRow = {
  at: string;
  label: string;
  responseTimeMs: number | null;
};

export function TsePerformanceChart({
  data,
  loading,
  slowThresholdMs = 3000,
  criticalThresholdMs = 10000,
  height = 240,
}: Props) {
  const { t } = useI18n();

  const chartData = useMemo(() => {
    const points = data ?? [];
    return points
      .filter((p) => p.responseTimeMs != null)
      .map(
        (p): ChartRow => ({
          at: p.timestamp,
          label: formatUtcDateTime(p.timestamp),
          responseTimeMs: p.responseTimeMs ?? null,
        })
      )
      .sort((a, b) => String(a.at).localeCompare(String(b.at)));
  }, [data]);

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  if (chartData.length === 0) {
    return <Empty description={t('tseFailover.performanceEmpty')} />;
  }

  return (
    <div style={{ width: '100%', height }}>
      <ResponsiveContainer>
        <LineChart data={chartData} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" minTickGap={32} tick={{ fontSize: 11 }} />
          <YAxis
            tick={{ fontSize: 11 }}
            unit=" ms"
            label={{
              value: t('tseFailover.performanceLatencyAxis'),
              angle: -90,
              position: 'insideLeft',
              style: { fontSize: 11 },
            }}
          />
          <Tooltip
            formatter={(value) => [
              typeof value === 'number' ? `${value} ms` : String(value ?? '—'),
              t('tseFailover.performanceLatencyAxis'),
            ]}
          />
          <Legend />
          <ReferenceLine
            y={slowThresholdMs}
            stroke="#faad14"
            strokeDasharray="4 4"
            label={t('tseFailover.performanceSlowThreshold')}
          />
          <ReferenceLine
            y={criticalThresholdMs}
            stroke="#ff4d4f"
            strokeDasharray="4 4"
            label={t('tseFailover.performanceCriticalThreshold')}
          />
          <Line
            type="monotone"
            dataKey="responseTimeMs"
            name={t('tseFailover.performanceLatencyAxis')}
            stroke="#1677ff"
            dot={false}
            strokeWidth={2}
            connectNulls={false}
          />
        </LineChart>
      </ResponsiveContainer>
      <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
        {t('tseFailover.performanceChartHint')}
      </Typography.Paragraph>
    </div>
  );
}
