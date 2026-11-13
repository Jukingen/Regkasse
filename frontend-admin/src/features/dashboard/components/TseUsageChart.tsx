'use client';

import React from 'react';
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

export type TseUsageChartPoint = {
  label: string;
  signatures: number;
};

type Props = {
  data: TseUsageChartPoint[];
  signaturesLabel: string;
};

export default function TseUsageChart({ data, signaturesLabel }: Props) {
  if (data.length === 0) {
    return null;
  }

  return (
    <ResponsiveContainer width="100%" height={180}>
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
        <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
        <YAxis tick={{ fontSize: 11 }} width={40} allowDecimals={false} />
        <Tooltip formatter={(value) => [Number(value ?? 0), signaturesLabel]} />
        <Line type="monotone" dataKey="signatures" stroke="#1677ff" strokeWidth={2} dot={false} />
      </LineChart>
    </ResponsiveContainer>
  );
}
