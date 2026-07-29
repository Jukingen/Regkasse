'use client';

import { Card, Col, Empty, Row } from 'antd';
import React from 'react';
import {
  Area,
  AreaChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

export type TseAnalyticsTrendPoint = {
  label: string;
  value: number;
};

export type TseAnalyticsDistributionSlice = {
  name: string;
  value: number;
};

export type TseAnalyticsChartsProps = {
  section: 'overview' | 'devices';
  txnChart: TseAnalyticsTrendPoint[];
  healthChart: TseAnalyticsTrendPoint[];
  deviceDistribution: {
    providers: TseAnalyticsDistributionSlice[];
    statuses: TseAnalyticsDistributionSlice[];
  };
  labels: {
    transactionTrend: string;
    healthTrend: string;
    providerBreakdown: string;
    statusBreakdown: string;
    totalTransactions: string;
    healthScore: string;
  };
  pieColors: string[];
};

export default function TseAnalyticsCharts({
  section,
  txnChart,
  healthChart,
  deviceDistribution,
  labels,
  pieColors,
}: TseAnalyticsChartsProps) {
  if (section === 'overview') {
    return (
      <>
        <Card size="small" title={labels.transactionTrend} style={{ marginTop: 16 }}>
          {txnChart.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <AreaChart data={txnChart}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} minTickGap={24} />
                  <YAxis tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="value"
                    stroke="#1677ff"
                    fill="#1677ff33"
                    name={labels.totalTransactions}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>

        <Card size="small" title={labels.healthTrend} style={{ marginTop: 16 }}>
          {healthChart.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <AreaChart data={healthChart}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} minTickGap={24} />
                  <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="value"
                    stroke="#52c41a"
                    fill="#52c41a33"
                    name={labels.healthScore}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </>
    );
  }

  return (
    <Row gutter={16}>
      <Col xs={24} md={12}>
        <Card size="small" title={labels.providerBreakdown}>
          {deviceDistribution.providers.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 280 }}>
              <ResponsiveContainer>
                <PieChart>
                  <Pie
                    data={deviceDistribution.providers}
                    dataKey="value"
                    nameKey="name"
                    outerRadius={90}
                    label
                  >
                    {deviceDistribution.providers.map((_, index) => (
                      <Cell
                        key={`prov-${index}`}
                        fill={pieColors[index % pieColors.length]}
                      />
                    ))}
                  </Pie>
                  <Tooltip />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
      <Col xs={24} md={12}>
        <Card size="small" title={labels.statusBreakdown}>
          {deviceDistribution.statuses.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 280 }}>
              <ResponsiveContainer>
                <PieChart>
                  <Pie
                    data={deviceDistribution.statuses}
                    dataKey="value"
                    nameKey="name"
                    outerRadius={90}
                    label
                  >
                    {deviceDistribution.statuses.map((_, index) => (
                      <Cell
                        key={`st-${index}`}
                        fill={pieColors[index % pieColors.length]}
                      />
                    ))}
                  </Pie>
                  <Tooltip />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
    </Row>
  );
}
