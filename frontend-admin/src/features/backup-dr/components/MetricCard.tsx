"use client";

/**
 * Backup izleme metrik kartı: durum rengi, ikon ve isteğe bağlı trend.
 */

import React from "react";
import { Card, Statistic } from "antd";
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  InfoCircleOutlined,
} from "@ant-design/icons";
import type { MetricStatus } from "@/features/backup-dr/logic/backupMonitoringMetrics";

export type { MetricStatus };

export interface MetricCardProps {
  title: string;
  value: string | number;
  status?: MetricStatus;
  trend?: number;
  trendLabel?: string;
  loading?: boolean;
}

function metricValueStyle(status: MetricStatus | undefined): React.CSSProperties | undefined {
  if (status === "error") return { color: "#ff4d4f" };
  if (status === "warning") return { color: "#faad14" };
  if (status === "success") return { color: "#52c41a" };
  if (status === "info") return { color: "#1677ff" };
  return undefined;
}

function metricPrefix(status: MetricStatus | undefined): React.ReactNode {
  if (status === "success") return <CheckCircleOutlined />;
  if (status === "error") return <CloseCircleOutlined />;
  if (status === "warning") return <ExclamationCircleOutlined />;
  if (status === "info") return <InfoCircleOutlined />;
  return undefined;
}

export function MetricCard({
  title,
  value,
  status,
  trend,
  trendLabel,
  loading,
}: MetricCardProps) {
  return (
    <Card size="small" className="backup-metric-card">
      <Statistic
        title={title}
        value={value}
        loading={loading}
        styles={{ content: metricValueStyle(status) }}
        prefix={metricPrefix(status)}
      />
      {trend !== undefined ? (
        <div
          className={`backup-metric-trend ${trend >= 0 ? "backup-metric-trend--positive" : "backup-metric-trend--negative"}`}
          style={{ marginTop: 8, fontSize: 12 }}
        >
          {trend >= 0 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}{" "}
          {Math.abs(trend)}% {trendLabel ?? ""}
        </div>
      ) : null}
    </Card>
  );
}
