'use client';

/**
 * Recharts bar chart for license activation buckets (client-only to avoid SSR issues).
 */
import React from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

export type LicenseDashboardChartDatum = {
  name: string;
  count: number;
};

export default function LicenseDashboardBarChart({ data }: { data: LicenseDashboardChartDatum[] }) {
  return (
    <ResponsiveContainer width="100%" height={280}>
      <BarChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
        <XAxis dataKey="name" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
        <YAxis allowDecimals={false} width={36} tick={{ fontSize: 11 }} />
        <Tooltip formatter={(v) => [Number(v ?? 0), '']} labelStyle={{ fontSize: 12 }} />
        <Bar dataKey="count" fill="#1677ff" radius={[4, 4, 0, 0]} maxBarSize={48} />
      </BarChart>
    </ResponsiveContainer>
  );
}
