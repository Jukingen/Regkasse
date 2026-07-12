"use client";

/**
 * Başarılı backup koşularının süre trendi (saniye).
 */

import React from "react";
import { Card, Empty } from "antd";
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { BackupDurationChartPoint } from "@/features/backup-dr/logic/backupMonitoringMetrics";

export interface BackupDurationChartProps {
  title: string;
  data: BackupDurationChartPoint[];
  durationSuffix: string;
}

export function BackupDurationChart({
  title,
  data,
  durationSuffix,
}: BackupDurationChartProps) {
  return (
    <Card size="small" title={title}>
      {data.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="—" />
      ) : (
        <ResponsiveContainer width="100%" height={220}>
          <LineChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 4 }}>
            <CartesianGrid strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="label" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
            <YAxis tick={{ fontSize: 11 }} width={40} />
            <Tooltip formatter={(v) => [`${Number(v ?? 0)} ${durationSuffix}`, ""]} />
            <Line
              type="monotone"
              dataKey="durationSec"
              stroke="#1677ff"
              dot={{ r: 3 }}
              strokeWidth={2}
            />
          </LineChart>
        </ResponsiveContainer>
      )}
    </Card>
  );
}
