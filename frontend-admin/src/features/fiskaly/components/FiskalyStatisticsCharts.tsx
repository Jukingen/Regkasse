'use client';

import { Card, Col, Empty, Row } from 'antd';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

export type FiskalyChartCount = { name: string; value: number };
export type FiskalyChartDaily = { date: string; total: number; success: number; failed: number };
export type FiskalyChartMonthly = { yearMonth: string; total: number };

export type FiskalyStatisticsChartsProps = {
  daily: FiskalyChartDaily[];
  byType: FiskalyChartCount[];
  successFailed: FiskalyChartCount[];
  monthly: FiskalyChartMonthly[];
  labels: {
    daily: string;
    byType: string;
    successFailed: string;
    monthly: string;
    total: string;
    success: string;
    failed: string;
  };
  pieColors: string[];
};

export default function FiskalyStatisticsCharts({
  daily,
  byType,
  successFailed,
  monthly,
  labels,
  pieColors,
}: FiskalyStatisticsChartsProps) {
  const hasDaily = daily.some((row) => row.total > 0);
  const hasMonthly = monthly.some((row) => row.total > 0);

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.daily}>
          {!hasDaily ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <LineChart data={daily}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" tick={{ fontSize: 11 }} minTickGap={24} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Legend />
                  <Line type="monotone" dataKey="total" name={labels.total} stroke="#1677ff" dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.byType}>
          {byType.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <PieChart>
                  <Pie data={byType} dataKey="value" nameKey="name" outerRadius={90} label>
                    {byType.map((_, index) => (
                      <Cell key={`type-${index}`} fill={pieColors[index % pieColors.length]} />
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
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.successFailed}>
          {successFailed.every((row) => row.value === 0) ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <BarChart data={successFailed}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Bar dataKey="value" name={labels.total}>
                    {successFailed.map((row, index) => (
                      <Cell
                        key={row.name}
                        fill={index === 0 ? '#52c41a' : '#cf1322'}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.monthly}>
          {!hasMonthly ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <AreaChart data={monthly}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="yearMonth" tick={{ fontSize: 11 }} minTickGap={16} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Area
                    type="monotone"
                    dataKey="total"
                    name={labels.total}
                    stroke="#722ed1"
                    fill="#722ed133"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
    </Row>
  );
}
