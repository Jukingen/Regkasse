'use client';

/**
 * Recharts line chart for license activation / usage trend (client-only).
 */
import React from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

export type LicenseUsageTrendDatum = {
  name: string;
  count: number;
};

export default function LicenseUsageTrendChart({ data }: { data: LicenseUsageTrendDatum[] }) {
  return (
    <ResponsiveContainer width="100%" height={280}>
      <LineChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
        <XAxis dataKey="name" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
        <YAxis allowDecimals={false} width={36} tick={{ fontSize: 11 }} />
        <Tooltip formatter={(v) => [Number(v ?? 0), '']} labelStyle={{ fontSize: 12 }} />
        <Line
          type="monotone"
          dataKey="count"
          stroke="#1677ff"
          strokeWidth={2}
          dot={{ r: 3 }}
          activeDot={{ r: 5 }}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}
