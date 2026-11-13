'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Skeleton,
  Space,
  Statistic,
  Tabs,
  Typography,
} from 'antd';
import dynamic from 'next/dynamic';
import { useMemo } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  downloadBase64File,
  exportTseBiReport,
  getTseBiDashboard,
} from '@/features/tse-analytics/api/analytics';
import {
  TseActiveTenantTag,
  TseTenantRequiredAlert,
} from '@/features/tse-shared/components/TseTenantContextUi';
import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const TseAnalyticsCharts = dynamic(
  () => import('@/features/tse-analytics/components/TseAnalyticsCharts'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 6 }} /> }
);

const KEY = ['admin', 'tse-analytics'] as const;
const PIE_COLORS = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#13c2c2', '#eb2f96', '#cf1322'];

export default function TseAnalyticsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { tenantId, isReady } = useTsePageTenant();

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

  const chartLabels = useMemo(
    () => ({
      transactionTrend: t('tseAnalytics.transactionTrend'),
      healthTrend: t('tseAnalytics.healthTrend'),
      providerBreakdown: t('tseAnalytics.providerBreakdown'),
      statusBreakdown: t('tseAnalytics.statusBreakdown'),
      totalTransactions: t('tseAnalytics.totalTransactions'),
      healthScore: t('tseAnalytics.healthScore'),
    }),
    [t]
  );

  if (!allowed) {
    return <Alert type="error" showIcon title={t('tseAnalytics.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAnalytics.title')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'securityTse', { title: t('tseAnalytics.title') })}
        extra={<TseActiveTenantTag />}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseAnalytics.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!isReady ? (
        <TseTenantRequiredAlert emptySelectKey="tseAnalytics.emptySelect" />
      ) : dashboardQuery.isError ? (
        <Alert type="error" showIcon title={t('tseAnalytics.loadError')} />
      ) : (
        <Card title={t('tseAnalytics.cardTitle')} loading={dashboardQuery.isLoading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('tseAnalytics.diagnosticNote')}
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

                    <TseAnalyticsCharts
                      section="overview"
                      txnChart={txnChart}
                      healthChart={healthChart}
                      deviceDistribution={deviceDistribution}
                      labels={chartLabels}
                      pieColors={PIE_COLORS}
                    />
                  </>
                ),
              },
              {
                key: 'devices',
                label: t('tseAnalytics.tabDevices'),
                children: (
                  <TseAnalyticsCharts
                    section="devices"
                    txnChart={txnChart}
                    healthChart={healthChart}
                    deviceDistribution={deviceDistribution}
                    labels={chartLabels}
                    pieColors={PIE_COLORS}
                  />
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
