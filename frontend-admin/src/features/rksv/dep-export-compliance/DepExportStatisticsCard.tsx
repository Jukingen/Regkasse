'use client';

import { BarChartOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Row, Space, Statistic, Tabs, Typography } from 'antd';
import React from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import {
  averageExportSizeMb,
  useDepExportForecast,
  useDepExportStatistics,
  useDepExportTrend,
} from '@/features/rksv/hooks/useDepExportStatistics';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type Props = {
  style?: React.CSSProperties;
};

export function DepExportStatisticsCard({ style }: Props) {
  const { t } = useI18n();
  const statsQuery = useDepExportStatistics();
  const trendQuery = useDepExportTrend(12);
  const forecastQuery = useDepExportForecast();

  const stats = statsQuery.data;
  const isFetching = statsQuery.isFetching || trendQuery.isFetching || forecastQuery.isFetching;
  const loadError = statsQuery.error ?? trendQuery.error ?? forecastQuery.error;

  const refresh = () => {
    void statsQuery.refetch();
    void trendQuery.refetch();
    void forecastQuery.refetch();
  };

  const trendData = (trendQuery.data ?? []).map((p) => ({
    label: p.label,
    total: p.totalExports,
    success: p.successfulExports,
    failed: p.failedExports,
  }));

  const forecastData = (forecastQuery.data?.points ?? []).map((p) => ({
    label: p.label,
    projected: p.projectedExports,
    due: p.hasKnownDueDate ? p.projectedExports : 0,
  }));

  return (
    <Card
      title={
        <Space>
          <BarChartOutlined />
          <span>{t('rksvHub.depExportStatistics.title')}</span>
        </Space>
      }
      loading={statsQuery.isLoading && !stats}
      style={style}
      extra={
        <Button icon={<ReloadOutlined />} loading={isFetching} onClick={refresh}>
          {t('rksvHub.depExportStatistics.refresh')}
        </Button>
      }
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('rksvHub.depExportStatistics.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={loadError}
              logContext="DepExportStatistics.load"
              fallbackKey="rksvHub.depExportStatistics.loadFailed"
            />
          }
        />
      ) : null}

      <Tabs
        items={[
          {
            key: 'overview',
            label: t('rksvHub.depExportStatistics.tabOverview'),
            children: (
              <Row gutter={[16, 16]}>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.totalExports')}
                    value={stats?.totalExports ?? 0}
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.successRate')}
                    value={stats?.successRate ?? 0}
                    precision={1}
                    suffix="%"
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.totalStorage')}
                    value={stats?.totalStorageUsedMb ?? 0}
                    precision={2}
                    suffix={t('rksvHub.depExportStatistics.sizeSuffixMb')}
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.lastExport')}
                    value={
                      stats?.lastExportDate
                        ? dayjs(stats.lastExportDate).fromNow()
                        : t('rksvHub.depExportStatistics.never')
                    }
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.avgSize')}
                    value={averageExportSizeMb(stats?.averageExportSizeBytes ?? 0)}
                    precision={2}
                    suffix={t('rksvHub.depExportStatistics.sizeSuffixMb')}
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.successful')}
                    value={stats?.successfulExports ?? 0}
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.failed')}
                    value={stats?.failedExports ?? 0}
                  />
                </Col>
                <Col xs={12} sm={6}>
                  <Statistic
                    title={t('rksvHub.depExportStatistics.nextDue')}
                    value={
                      stats?.nextDueDate
                        ? dayjs(stats.nextDueDate).format('YYYY-MM-DD')
                        : t('rksvHub.depExportStatistics.noDue')
                    }
                  />
                </Col>
              </Row>
            ),
          },
          {
            key: 'trend',
            label: t('rksvHub.depExportStatistics.tabTrend'),
            children: (
              <div style={{ minHeight: 260 }}>
                {trendData.length === 0 ? (
                  <Typography.Text type="secondary">
                    {t('rksvHub.depExportStatistics.emptyTrend')}
                  </Typography.Text>
                ) : (
                  <ResponsiveContainer width="100%" height={260}>
                    <LineChart data={trendData}>
                      <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                      <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                      <YAxis allowDecimals={false} tick={{ fontSize: 11 }} width={40} />
                      <Tooltip />
                      <Legend />
                      <Line
                        type="monotone"
                        dataKey="success"
                        name={t('rksvHub.depExportStatistics.chartSuccess')}
                        stroke="#52c41a"
                        strokeWidth={2}
                        dot={false}
                      />
                      <Line
                        type="monotone"
                        dataKey="failed"
                        name={t('rksvHub.depExportStatistics.chartFailed')}
                        stroke="#cf1322"
                        strokeWidth={2}
                        dot={false}
                      />
                      <Line
                        type="monotone"
                        dataKey="total"
                        name={t('rksvHub.depExportStatistics.chartTotal')}
                        stroke="#1677ff"
                        strokeWidth={2}
                        dot={false}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                )}
              </div>
            ),
          },
          {
            key: 'forecast',
            label: t('rksvHub.depExportStatistics.tabForecast'),
            children: (
              <div>
                {forecastQuery.data?.nextRequirementTitle ? (
                  <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                    {t('rksvHub.depExportStatistics.nextRequirement', {
                      title: forecastQuery.data.nextRequirementTitle,
                      date: forecastQuery.data.nextDueDate
                        ? dayjs(forecastQuery.data.nextDueDate).format('YYYY-MM-DD')
                        : '—',
                    })}
                  </Typography.Paragraph>
                ) : null}
                <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                  {t('rksvHub.depExportStatistics.avgMonthly', {
                    value: forecastQuery.data?.averageMonthlyExports ?? 0,
                  })}
                </Typography.Paragraph>
                {forecastData.length === 0 ? (
                  <Typography.Text type="secondary">
                    {t('rksvHub.depExportStatistics.emptyForecast')}
                  </Typography.Text>
                ) : (
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={forecastData}>
                      <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.06)" />
                      <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                      <YAxis allowDecimals tick={{ fontSize: 11 }} width={40} />
                      <Tooltip />
                      <Legend />
                      <Bar
                        dataKey="projected"
                        name={t('rksvHub.depExportStatistics.chartProjected')}
                        fill="#1677ff"
                        radius={[4, 4, 0, 0]}
                      />
                    </BarChart>
                  </ResponsiveContainer>
                )}
                <Typography.Paragraph
                  type="secondary"
                  style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}
                >
                  {forecastQuery.data?.method ?? t('rksvHub.depExportStatistics.disclaimer')}
                </Typography.Paragraph>
              </div>
            ),
          },
        ]}
      />
    </Card>
  );
}
