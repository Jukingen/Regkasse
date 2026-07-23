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

import type { TseHealthTrendPoint } from '../types';

type Props = {
  data: TseHealthTrendPoint[] | undefined;
  loading?: boolean;
  healthyMinScore?: number;
  degradedMinScore?: number;
  height?: number;
};

type ChartRow = {
  at: string;
  label: string;
} & Record<string, number | string>;

const SERIES_COLORS = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#13c2c2', '#eb2f96'];

export function TseHealthTrendChart({
  data,
  loading,
  healthyMinScore = 80,
  degradedMinScore = 50,
  height = 280,
}: Props) {
  const { t } = useI18n();

  const { chartData, seriesKeys } = useMemo(() => {
    const points = data ?? [];
    if (points.length === 0) {
      return { chartData: [] as ChartRow[], seriesKeys: [] as string[] };
    }

    const keys = Array.from(
      new Set(points.map((p) => p.deviceLabel || p.deviceId))
    );
    const byTime = new Map<string, ChartRow>();

    for (const point of points) {
      const iso = point.date;
      const key = point.deviceLabel || point.deviceId;
      let row = byTime.get(iso);
      if (!row) {
        row = {
          at: iso,
          label: formatUtcDateTime(iso),
        };
        byTime.set(iso, row);
      }
      row[key] = point.score;
    }

    const chartData = Array.from(byTime.values()).sort((a, b) =>
      String(a.at).localeCompare(String(b.at))
    );
    return { chartData, seriesKeys: keys };
  }, [data]);

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  if (chartData.length === 0) {
    return <Empty description={t('tseFailover.trendEmpty')} />;
  }

  return (
    <div style={{ width: '100%', height }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {t('tseFailover.trendThresholdHealthy')}: {healthyMinScore} ·{' '}
        {t('tseFailover.trendThresholdDegraded')}: {degradedMinScore}
      </Typography.Text>
      <ResponsiveContainer width="100%" height={height - 24}>
        <LineChart data={chartData} margin={{ top: 16, right: 16, left: 0, bottom: 8 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
          <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" minTickGap={32} />
          <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} width={40} />
          <Tooltip />
          <Legend />
          <ReferenceLine
            y={healthyMinScore}
            stroke="#52c41a"
            strokeDasharray="4 4"
            label={{ value: String(healthyMinScore), position: 'right', fontSize: 11 }}
          />
          <ReferenceLine
            y={degradedMinScore}
            stroke="#faad14"
            strokeDasharray="4 4"
            label={{ value: String(degradedMinScore), position: 'right', fontSize: 11 }}
          />
          {seriesKeys.map((key, index) => (
            <Line
              key={key}
              type="monotone"
              dataKey={key}
              name={key}
              stroke={SERIES_COLORS[index % SERIES_COLORS.length]}
              strokeWidth={2}
              dot={false}
              connectNulls
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
