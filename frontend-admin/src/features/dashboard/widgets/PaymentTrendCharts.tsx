'use client';

import React from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import type { TrendPeriod } from '@/features/payments/types/paymentTrends';

export type PaymentTrendChartDatum = {
  label: string;
  revenue: number;
  count: number;
};

type Props = {
  data: PaymentTrendChartDatum[];
  period: TrendPeriod;
  revenueLabel: string;
  countLabel: string;
};

export default function PaymentTrendCharts({ data, period, revenueLabel, countLabel }: Props) {
  return (
    <ResponsiveContainer width="100%" height={220}>
      {period === 'Daily' ? (
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
          <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
          <YAxis tick={{ fontSize: 11 }} width={48} />
          <Tooltip
            formatter={(value, name) => {
              const n = Number(value ?? 0);
              return name === 'revenue'
                ? [`€${n.toFixed(2)}`, revenueLabel]
                : [n, countLabel];
            }}
          />
          <Line
            type="monotone"
            dataKey="revenue"
            stroke="#1677ff"
            strokeWidth={2}
            dot={false}
          />
        </LineChart>
      ) : (
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
          <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
          <YAxis tick={{ fontSize: 11 }} width={48} />
          <Tooltip
            formatter={(value) => [`€${Number(value ?? 0).toFixed(2)}`, revenueLabel]}
          />
          <Bar dataKey="revenue" fill="#1677ff" radius={[4, 4, 0, 0]} maxBarSize={40} />
        </BarChart>
      )}
    </ResponsiveContainer>
  );
}
