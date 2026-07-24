'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Row,
  Select,
  Space,
  Statistic,
  Tabs,
  Typography,
} from 'antd';
import { useMemo, useState } from 'react';
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

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  downloadBase64File,
  exportTseBiReport,
  getTseBiDashboard,
} from '@/features/tse-analytics/api/analytics';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-analytics'] as const;
const PIE_COLORS = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#13c2c2', '#eb2f96', '#cf1322'];

export default function TseAnalyticsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-analytics'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const dashboardQuery = useQuery({
    queryKey: [...KEY, 'dashboard', tenantId],
    queryFn: ({ signal }) => getTseBiDashboard(tenantId!, 30, signal),
    enabled: allowed && !!tenantId,
  });

  const exportMutation = useMutation({
    mutationFn: (format: 'csv' | 'pdf') => exportTseBiReport(tenantId!, format, 30),
    onSuccess: (result) => {
      downloadBase64File(result.fileName, result.contentType, result.contentBase64);
      notify.success(t('tseAnalytics.exportSuccess'));
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAnalytics.export',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const dashboard = dashboardQuery.data;
  const alertTotal = (dashboard?.criticalAlerts ?? 0) + (dashboard?.warningAlerts ?? 0);

  const txnChart = useMemo(
    () =>
      (dashboard?.transactionTrends ?? []).map((row) => ({
        label: row.label,
        value: row.value,
      })),
    [dashboard?.transactionTrends]
  );

  const healthChart = useMemo(
    () =>
      (dashboard?.healthTrends ?? []).map((row) => ({
        label: row.label,
        value: row.value,
      })),
    [dashboard?.healthTrends]
  );

  const deviceDistribution = useMemo(() => {
    const providers = dashboard?.providerBreakdown ?? [];
    const statuses = dashboard?.statusBreakdown ?? [];
    return {
      providers: providers.map((p) => ({ name: p.name, value: p.count })),
      statuses: statuses.map((s) => ({ name: s.name, value: s.count })),
    };
  }, [dashboard?.providerBreakdown, dashboard?.statusBreakdown]);

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseAnalytics.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAnalytics.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseAnalytics.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseAnalytics.tenantLabel')}
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
          {t('tseAnalytics.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseAnalytics.emptySelect')} />
      ) : dashboardQuery.isError ? (
        <Alert type="error" showIcon message={t('tseAnalytics.loadError')} />
      ) : (
        <Card title={t('tseAnalytics.cardTitle')} loading={dashboardQuery.isLoading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseAnalytics.diagnosticNote')}
          />

          <Tabs
            items={[
              {
                key: 'overview',
                label: t('tseAnalytics.tabOverview'),
                children: (
                  <>
                    <Row gutter={16}>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseAnalytics.totalTransactions')}
                          value={dashboard?.totalTransactions ?? 0}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseAnalytics.activeDevices')}
                          value={dashboard?.activeDevices ?? 0}
                          suffix={`/ ${dashboard?.totalDevices ?? 0}`}
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseAnalytics.healthScore')}
                          value={dashboard?.overallHealthScore ?? 0}
                          suffix="%"
                        />
                      </Col>
                      <Col xs={24} sm={12} md={6}>
                        <Statistic
                          title={t('tseAnalytics.alerts')}
                          value={alertTotal}
                          valueStyle={{
                            color:
                              (dashboard?.criticalAlerts ?? 0) > 0 ? '#cf1322' : '#52c41a',
                          }}
                        />
                      </Col>
                    </Row>

                    <Card
                      size="small"
                      title={t('tseAnalytics.transactionTrend')}
                      style={{ marginTop: 16 }}
                    >
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
                                name={t('tseAnalytics.totalTransactions')}
                              />
                            </AreaChart>
                          </ResponsiveContainer>
                        </div>
                      )}
                    </Card>

                    <Card
                      size="small"
                      title={t('tseAnalytics.healthTrend')}
                      style={{ marginTop: 16 }}
                    >
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
                                name={t('tseAnalytics.healthScore')}
                              />
                            </AreaChart>
                          </ResponsiveContainer>
                        </div>
                      )}
                    </Card>
                  </>
                ),
              },
              {
                key: 'devices',
                label: t('tseAnalytics.tabDevices'),
                children: (
                  <Row gutter={16}>
                    <Col xs={24} md={12}>
                      <Card size="small" title={t('tseAnalytics.providerBreakdown')}>
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
                                      fill={PIE_COLORS[index % PIE_COLORS.length]}
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
                      <Card size="small" title={t('tseAnalytics.statusBreakdown')}>
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
                                      fill={PIE_COLORS[index % PIE_COLORS.length]}
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
                ),
              },
              {
                key: 'export',
                label: t('tseAnalytics.tabExport'),
                children: (
                  <Space wrap>
                    <Button
                      type="primary"
                      loading={exportMutation.isPending}
                      onClick={() => exportMutation.mutate('pdf')}
                    >
                      {t('tseAnalytics.exportPdf')}
                    </Button>
                    <Button
                      loading={exportMutation.isPending}
                      onClick={() => exportMutation.mutate('csv')}
                    >
                      {t('tseAnalytics.exportCsv')}
                    </Button>
                  </Space>
                ),
              },
            ]}
          />
        </Card>
      )}
    </>
  );
}
