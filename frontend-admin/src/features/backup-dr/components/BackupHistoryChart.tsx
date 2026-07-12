"use client";

/**
 * 30 günlük backup geçmişi: başarı/başarısızlık çubukları + süre çizgisi (çift Y ekseni).
 */

import React, { useMemo, useState } from "react";
import { Card, Empty } from "antd";
import type { BackupRunResponseDto } from "@/api/generated/model";
import {
  Bar,
  Brush,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { buildBackupHistory30DayChartData } from "@/features/backup-dr/logic/backupMonitoringMetrics";
import { formatUserMonthDay } from "@/lib/dateFormatter";

export interface BackupHistoryChartPoint {
  key: string;
  date: string;
  success: number;
  failed: number;
  duration: number;
  runId?: string;
}

export interface BackupHistoryChartProps {
  /** Client-derived series when API history is unavailable. */
  runs?: readonly BackupRunResponseDto[];
  /** Preferred when <c>GET dashboard/stats</c> history is available. */
  chartData?: readonly BackupHistoryChartPoint[];
  formatLocale: string;
  title: string;
  successLabel: string;
  failedLabel: string;
  durationLabel: string;
  durationSuffix: string;
  onBarClick?: (runId: string) => void;
}

export function BackupHistoryChart({
  runs = [],
  chartData: chartDataProp,
  formatLocale,
  title,
  successLabel,
  failedLabel,
  durationLabel,
  durationSuffix,
  onBarClick,
}: BackupHistoryChartProps) {
  const formatDate = (iso: string) => formatUserMonthDay(iso) || "—";

  const chartData = useMemo(() => {
    if (chartDataProp && chartDataProp.length > 0) return [...chartDataProp];
    return buildBackupHistory30DayChartData(runs, formatDate);
  }, [chartDataProp, runs, formatLocale]);

  const [brushRange, setBrushRange] = useState<{ startIndex?: number; endIndex?: number }>({});

  const displayData = useMemo(() => {
    if (brushRange.startIndex === undefined || brushRange.endIndex === undefined) {
      return chartData;
    }
    return chartData.slice(brushRange.startIndex, brushRange.endIndex + 1);
  }, [chartData, brushRange.endIndex, brushRange.startIndex]);

  const handleBarClick = (payload: BackupHistoryChartPoint | undefined) => {
    const runId = payload?.runId ?? payload?.key;
    if (runId && onBarClick) onBarClick(runId);
  };

  return (
    <Card size="small" title={title}>
      {chartData.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="—" />
      ) : (
        <ResponsiveContainer width="100%" height={320}>
          <ComposedChart data={displayData} margin={{ top: 8, right: 12, left: 0, bottom: 4 }}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" tick={{ fontSize: 11 }} interval="preserveStartEnd" />
            <YAxis
              yAxisId="left"
              allowDecimals={false}
              domain={[0, 1]}
              tick={{ fontSize: 11 }}
            />
            <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 11 }} />
            <Tooltip
              formatter={(value, name) => {
                const n = Number(value ?? 0);
                if (name === durationLabel) {
                  return [`${n} ${durationSuffix}`, durationLabel];
                }
                return [n, name];
              }}
            />
            <Legend />
            <Brush
              dataKey="date"
              height={24}
              stroke="#1677ff"
              travellerWidth={8}
              onChange={(range) => {
                if (range && typeof range.startIndex === "number") {
                  setBrushRange({
                    startIndex: range.startIndex,
                    endIndex: range.endIndex,
                  });
                }
              }}
            />
            <Bar
              yAxisId="left"
              dataKey="success"
              fill="#52c41a"
              name={successLabel}
              radius={[2, 2, 0, 0]}
              cursor={onBarClick ? "pointer" : undefined}
              onClick={(data) => handleBarClick(data as unknown as BackupHistoryChartPoint)}
            />
            <Bar
              yAxisId="left"
              dataKey="failed"
              fill="#ff4d4f"
              name={failedLabel}
              radius={[2, 2, 0, 0]}
              cursor={onBarClick ? "pointer" : undefined}
              onClick={(data) => handleBarClick(data as unknown as BackupHistoryChartPoint)}
            />
            <Line
              yAxisId="right"
              type="monotone"
              dataKey="duration"
              stroke="#1890ff"
              name={durationLabel}
              dot={{ r: 3 }}
              strokeWidth={2}
            />
          </ComposedChart>
        </ResponsiveContainer>
      )}
    </Card>
  );
}
