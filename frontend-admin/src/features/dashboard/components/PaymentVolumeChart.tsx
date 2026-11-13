'use client';

import React from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

export type PaymentVolumeChartPoint = {
  label: string;
  revenue: number;
};

type Props = {
  data: PaymentVolumeChartPoint[];
  revenueLabel: string;
};

export default function PaymentVolumeChart({ data, revenueLabel }: Props) {
  if (data.length === 0) {
    return null;
  }

  return (
    <ResponsiveContainer width="100%" height={180}>
      <BarChart data={data}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
        <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
        <YAxis tick={{ fontSize: 11 }} width={48} />
        <Tooltip formatter={(value) => [`€${Number(value ?? 0).toFixed(2)}`, revenueLabel]} />
        <Bar dataKey="revenue" fill="#1677ff" radius={[4, 4, 0, 0]} maxBarSize={32} />
      </BarChart>
    </ResponsiveContainer>
  );
}
