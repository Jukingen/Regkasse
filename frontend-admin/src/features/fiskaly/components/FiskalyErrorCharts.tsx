'use client';

import { Card, Col, Empty, Row } from 'antd';
import {
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

export type FiskalyErrorChartsProps = {
  trend: Array<{ date: string; count: number }>;
  byCode: Array<{ name: string; value: number }>;
  byType: Array<{ name: string; value: number }>;
  byTenant: Array<{ name: string; value: number }>;
  showTenant: boolean;
  labels: {
    trend: string;
    distribution: string;
    byType: string;
    byTenant: string;
    errors: string;
  };
  pieColors: string[];
};

export default function FiskalyErrorCharts({
  trend,
  byCode,
  byType,
  byTenant,
  showTenant,
  labels,
  pieColors,
}: FiskalyErrorChartsProps) {
  const hasTrend = trend.some((row) => row.count > 0);

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.trend}>
          {!hasTrend ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <LineChart data={trend}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" tick={{ fontSize: 11 }} minTickGap={24} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Legend />
                  <Line type="monotone" dataKey="count" name={labels.errors} stroke="#cf1322" dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card size="small" title={labels.distribution}>
          {byCode.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 260 }}>
              <ResponsiveContainer>
                <PieChart>
                  <Pie data={byCode} dataKey="value" nameKey="name" outerRadius={90} label>
                    {byCode.map((_, index) => (
                      <Cell key={`code-${index}`} fill={pieColors[index % pieColors.length]} />
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
      <Col xs={24} lg={showTenant ? 12 : 24}>
        <Card size="small" title={labels.byType}>
          {byType.length === 0 ? (
            <Empty />
          ) : (
            <div style={{ width: '100%', height: 280 }}>
              <ResponsiveContainer>
                <BarChart data={byType} margin={{ left: 8, right: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" tick={{ fontSize: 11 }} interval={0} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                  <Tooltip />
                  <Bar dataKey="value" name={labels.errors} fill="#1677ff" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      </Col>
      {showTenant ? (
        <Col xs={24} lg={12}>
          <Card size="small" title={labels.byTenant}>
            {byTenant.length === 0 ? (
              <Empty />
            ) : (
              <div style={{ width: '100%', height: 280 }}>
                <ResponsiveContainer>
                  <BarChart data={byTenant} margin={{ left: 8, right: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" tick={{ fontSize: 11 }} interval={0} />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                    <Tooltip />
                    <Bar dataKey="value" name={labels.errors} fill="#fa541c" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </Card>
        </Col>
      ) : null}
    </Row>
  );
}
