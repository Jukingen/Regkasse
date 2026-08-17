'use client';

import React from 'react';
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';

export type PlanSlice = {
  name: string;
  value: number;
};

const SLICE_COLORS = ['#8c8c8c', '#1677ff', '#13c2c2', '#722ed1'];

type Props = {
  data: PlanSlice[];
};

export default function PlanDistributionChart({ data }: Props) {
  const filtered = data.filter((d) => d.value > 0);
  if (filtered.length === 0) {
    return null;
  }

  return (
    <ResponsiveContainer width="100%" height={180}>
      <PieChart>
        <Pie data={filtered} dataKey="value" nameKey="name" innerRadius={48} outerRadius={72} paddingAngle={2}>
          {filtered.map((entry, index) => (
            <Cell key={entry.name} fill={SLICE_COLORS[index % SLICE_COLORS.length]} />
          ))}
        </Pie>
        <Tooltip formatter={(value) => [Number(value ?? 0), '']} />
      </PieChart>
    </ResponsiveContainer>
  );
}
