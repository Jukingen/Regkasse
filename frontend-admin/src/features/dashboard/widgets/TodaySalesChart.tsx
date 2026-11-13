'use client';

import React from 'react';
import {
  Line,
  LineChart,
  Tooltip as RechartsTooltip,
  ResponsiveContainer,
  XAxis,
  YAxis,
} from 'recharts';

export type TodaySalesChartDatum = {
  date: string;
  total: number;
};

export default function TodaySalesChart({ data }: { data: TodaySalesChartDatum[] }) {
  return (
    <ResponsiveContainer width="100%" height={120}>
      <LineChart data={data}>
        <XAxis dataKey="date" hide />
        <YAxis hide />
        <RechartsTooltip formatter={(v) => `€${Number(v ?? 0).toFixed(2)}`} />
        <Line type="monotone" dataKey="total" stroke="#1677ff" dot={false} />
      </LineChart>
    </ResponsiveContainer>
  );
}
