'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Card,
  Col,
  Empty,
  List,
  Progress,
  Row,
  Select,
  Statistic,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getTseCohortAnalysis,
  getTseFeatureUsageReport,
  getTseUserBehaviorReport,
} from '@/features/tse-user-analytics/api/userAnalytics';
import type {
  TseCohortRow,
  TseDropoffPoint,
  TseFeatureHeatmapCell,
} from '@/features/tse-user-analytics/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-user-analytics'] as const;

function severityColor(severity: string): string {
  switch (severity) {
    case 'High':
      return 'red';
    case 'Medium':
      return 'orange';
    default:
      return 'blue';
  }
}

export default function TseUserAnalyticsPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-user-analytics'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const reportQuery = useQuery({
    queryKey: [...KEY, 'report', tenantId],
    queryFn: ({ signal }) => getTseUserBehaviorReport(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const featuresQuery = useQuery({
    queryKey: [...KEY, 'features', tenantId],
    queryFn: ({ signal }) => getTseFeatureUsageReport(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const cohortQuery = useQuery({
    queryKey: [...KEY, 'cohorts', tenantId],
    queryFn: ({ signal }) => getTseCohortAnalysis(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const report = reportQuery.data;
  const features = featuresQuery.data;
  const cohorts = cohortQuery.data;

  const adoptionRate = useMemo(() => {
    const rates = Object.values(report?.featureAdoptionRate ?? {});
    if (rates.length === 0) return 0;
    return Math.round(rates.reduce((a, b) => a + b, 0) / rates.length);
  }, [report?.featureAdoptionRate]);

  const satisfactionScore = report?.userSatisfactionScores?.overall ?? 0;

  const heatmapColumns: ColumnsType<TseFeatureHeatmapCell> = [
    { title: t('tseUserAnalytics.colFeature'), dataIndex: 'feature', key: 'feature' },
    { title: t('tseUserAnalytics.colDay'), dataIndex: 'dayOfWeek', key: 'dayOfWeek' },
    { title: t('tseUserAnalytics.colCount'), dataIndex: 'count', key: 'count', width: 100 },
  ];

  const maxRetentionWeeks = useMemo(() => {
    const rows = cohorts?.cohorts ?? [];
    return rows.reduce((max, row) => Math.max(max, row.retentionByWeek.length), 0);
  }, [cohorts?.cohorts]);

  const cohortColumns: ColumnsType<TseCohortRow> = useMemo(() => {
    const cols: ColumnsType<TseCohortRow> = [
      { title: t('tseUserAnalytics.colCohort'), dataIndex: 'cohortWeek', key: 'cohortWeek' },
      { title: t('tseUserAnalytics.colSize'), dataIndex: 'cohortSize', key: 'cohortSize', width: 90 },
    ];
    for (let i = 0; i < maxRetentionWeeks; i += 1) {
      cols.push({
        title: t('tseUserAnalytics.colWeek').replace('{n}', String(i)),
        key: `w${i}`,
        width: 80,
        render: (_, row) => {
          const value = row.retentionByWeek[i];
          return value == null ? '—' : `${value}%`;
        },
      });
    }
    return cols;
  }, [maxRetentionWeeks, t]);

  const dropoffColumns: ColumnsType<TseDropoffPoint> = [
    { title: t('tseUserAnalytics.colFrom'), dataIndex: 'fromStep', key: 'fromStep' },
    { title: t('tseUserAnalytics.colTo'), dataIndex: 'toStep', key: 'toStep' },
    {
      title: t('tseUserAnalytics.colDropoff'),
      dataIndex: 'dropoffPercent',
      key: 'dropoffPercent',
      render: (v: number) => `${v}%`,
    },
    {
      title: t('tseUserAnalytics.colSeverity'),
      dataIndex: 'severity',
      key: 'severity',
      render: (v: string) => <Tag color={severityColor(v)}>{v}</Tag>,
    },
  ];

  const funnelChart = useMemo(
    () =>
      (report?.funnelSteps ?? []).map((step) => ({
        name: step.label || step.step,
        count: step.count,
        conversion: step.conversionPercent,
      })),
    [report?.funnelSteps]
  );

  const loading =
    reportQuery.isLoading || featuresQuery.isLoading || cohortQuery.isLoading;
  const hasError = reportQuery.isError || featuresQuery.isError || cohortQuery.isError;

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseUserAnalytics.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseUserAnalytics.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseUserAnalytics.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseUserAnalytics.tenantLabel')}
            loading={tenantsQuery.isLoading}
            value={tenantId}
            onChange={setTenantId}
            options={(tenantsQuery.data ?? []).map((tenant) => ({
              value: tenant.id,
              label: `${tenant.name} (${tenant.slug})`,
            }))}
          />
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseUserAnalytics.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseUserAnalytics.emptySelect')} />
      ) : hasError ? (
        <Alert type="error" showIcon message={t('tseUserAnalytics.loadError')} />
      ) : (
        <Card title={t('tseUserAnalytics.cardTitle')} loading={loading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseUserAnalytics.diagnosticNote')}
          />

          <Tabs
            items={[
              {
                key: 'overview',
                label: t('tseUserAnalytics.tabOverview'),
                children: (
                  <>
                    <Row gutter={16}>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseUserAnalytics.dau')}
                          value={report?.dailyActiveUsers ?? 0}
                          precision={1}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseUserAnalytics.avgSession')}
                          value={report?.averageSessionDuration ?? 0}
                          precision={1}
                          suffix={t('tseUserAnalytics.minutesSuffix')}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseUserAnalytics.featureAdoption')}
                          value={adoptionRate}
                          suffix="%"
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseUserAnalytics.satisfaction')}
                          value={satisfactionScore}
                          precision={1}
                          suffix="/10"
                        />
                      </Col>
                    </Row>

                    <Card
                      size="small"
                      title={t('tseUserAnalytics.heatmapTitle')}
                      style={{ marginTop: 16 }}
                    >
                      {(features?.heatmap ?? []).length === 0 ? (
                        <Empty description={t('tseUserAnalytics.noData')} />
                      ) : (
                        <Table
                          size="small"
                          rowKey={(row) => `${row.feature}-${row.dayOfWeek}`}
                          columns={heatmapColumns}
                          dataSource={features?.heatmap ?? []}
                          pagination={{ pageSize: 10 }}
                        />
                      )}
                    </Card>

                    <Card
                      size="small"
                      title={t('tseUserAnalytics.recommendationsTitle')}
                      style={{ marginTop: 16 }}
                    >
                      <List
                        dataSource={report?.recommendations ?? []}
                        locale={{ emptyText: t('tseUserAnalytics.noData') }}
                        renderItem={(item) => (
                          <List.Item>
                            <List.Item.Meta
                              title={
                                <SpaceTag severity={item.severity} title={item.title} />
                              }
                              description={item.description}
                            />
                          </List.Item>
                        )}
                      />
                    </Card>
                  </>
                ),
              },
              {
                key: 'cohort',
                label: t('tseUserAnalytics.tabCohort'),
                children:
                  (cohorts?.cohorts ?? []).length === 0 ? (
                    <Empty description={t('tseUserAnalytics.noData')} />
                  ) : (
                    <Table
                      size="small"
                      rowKey={(row) => row.cohortWeek}
                      columns={cohortColumns}
                      dataSource={cohorts?.cohorts ?? []}
                      pagination={false}
                      scroll={{ x: true }}
                    />
                  ),
              },
              {
                key: 'funnel',
                label: t('tseUserAnalytics.tabFunnel'),
                children: (
                  <>
                    <Card size="small" title={t('tseUserAnalytics.funnelTitle')}>
                      {funnelChart.length === 0 ? (
                        <Empty description={t('tseUserAnalytics.noData')} />
                      ) : (
                        <div style={{ width: '100%', height: 280 }}>
                          <ResponsiveContainer>
                            <BarChart data={funnelChart} layout="vertical" margin={{ left: 24 }}>
                              <CartesianGrid strokeDasharray="3 3" />
                              <XAxis type="number" allowDecimals={false} />
                              <YAxis type="category" dataKey="name" width={120} tick={{ fontSize: 12 }} />
                              <Tooltip />
                              <Bar dataKey="count" fill="#1677ff" name={t('tseUserAnalytics.colCount')} />
                            </BarChart>
                          </ResponsiveContainer>
                        </div>
                      )}
                      <Row gutter={16} style={{ marginTop: 16 }}>
                        {(report?.funnelSteps ?? []).map((step) => (
                          <Col xs={24} sm={12} md={8} key={step.step} style={{ marginBottom: 12 }}>
                            <Typography.Text strong>{step.label}</Typography.Text>
                            <Progress percent={Math.round(step.conversionPercent)} size="small" />
                          </Col>
                        ))}
                      </Row>
                    </Card>

                    <Card
                      size="small"
                      title={t('tseUserAnalytics.dropoffTitle')}
                      style={{ marginTop: 16 }}
                    >
                      <Table
                        size="small"
                        rowKey={(row) => `${row.fromStep}-${row.toStep}`}
                        columns={dropoffColumns}
                        dataSource={report?.dropoffPoints ?? []}
                        pagination={false}
                        locale={{ emptyText: t('tseUserAnalytics.noData') }}
                      />
                    </Card>
                  </>
                ),
              },
            ]}
          />
        </Card>
      )}
    </>
  );
}

function SpaceTag({ severity, title }: { severity: string; title: string }) {
  return (
    <span>
      <Tag color={severityColor(severity)}>{severity}</Tag>
      {title}
    </span>
  );
}
