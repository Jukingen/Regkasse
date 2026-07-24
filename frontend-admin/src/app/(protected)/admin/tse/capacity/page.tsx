'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  List,
  Progress,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  checkTseCapacityAlerts,
  getTseCapacityForecast,
  getTseCapacityReport,
} from '@/features/tse-capacity/api/capacity';
import type {
  TseCapacityReport,
  TseDailyTransactionTrend,
  TseForecastDayPoint,
} from '@/features/tse-capacity/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const CAPACITY_KEY = ['admin', 'tse-capacity'] as const;

export default function TseCapacityPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-capacity'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const reportQuery = useQuery({
    queryKey: [...CAPACITY_KEY, 'report', tenantId],
    queryFn: ({ signal }) => getTseCapacityReport(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const forecastQuery = useQuery({
    queryKey: [...CAPACITY_KEY, 'forecast', tenantId],
    queryFn: ({ signal }) => getTseCapacityForecast(tenantId!, 30, signal),
    enabled: allowed && !!tenantId,
  });

  const checkMutation = useMutation({
    mutationFn: () => checkTseCapacityAlerts(tenantId!),
    onSuccess: (alert) => {
      notify.success(t('tseCapacity.checkSuccess'));
      if (alert.hasAlert) {
        notify.warning(alert.message, { mode: 'notification' });
      }
      void reportQuery.refetch();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseCapacity.checkAlerts',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const report: TseCapacityReport | undefined = reportQuery.data;

  const trendColumns: ColumnsType<TseDailyTransactionTrend> = [
    {
      title: t('tseCapacity.colDate'),
      dataIndex: 'date',
      key: 'date',
      render: (d: string) => dayjs(d).format('YYYY-MM-DD'),
    },
    {
      title: t('tseCapacity.colTransactions'),
      dataIndex: 'transactionCount',
      key: 'transactionCount',
    },
    {
      title: t('tseCapacity.colSigned'),
      dataIndex: 'signedCount',
      key: 'signedCount',
    },
  ];

  const forecastColumns: ColumnsType<TseForecastDayPoint> = [
    {
      title: t('tseCapacity.colDate'),
      dataIndex: 'date',
      key: 'date',
      render: (d: string) => dayjs(d).format('YYYY-MM-DD'),
    },
    {
      title: t('tseCapacity.colForecast'),
      dataIndex: 'estimatedTransactions',
      key: 'estimatedTransactions',
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseCapacity.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseCapacity.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseCapacity.title') }]}
        extra={
          <Space>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 260 }}
              placeholder={t('tseCapacity.tenantLabel')}
              loading={tenantsQuery.isLoading}
              value={tenantId}
              onChange={setTenantId}
              options={(tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
              }))}
            />
            <Button
              disabled={!tenantId}
              loading={reportQuery.isFetching}
              onClick={() => {
                void reportQuery.refetch();
                void forecastQuery.refetch();
              }}
            >
              {t('tseCapacity.loadReport')}
            </Button>
            <Button
              type="primary"
              disabled={!tenantId}
              loading={checkMutation.isPending}
              onClick={() => checkMutation.mutate()}
            >
              {t('tseCapacity.checkAlerts')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseCapacity.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseCapacity.emptySelect')} />
      ) : (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Card title={t('tseCapacity.cardTitle')} loading={reportQuery.isLoading}>
            {report ? (
              <>
                <Row gutter={16}>
                  <Col span={6}>
                    <Statistic title={t('tseCapacity.dailyAvg')} value={report.dailyTransactionAverage} />
                  </Col>
                  <Col span={6}>
                    <Statistic
                      title={t('tseCapacity.utilization')}
                      value={report.currentUtilizationPercentage}
                      suffix="%"
                      valueStyle={{
                        color: report.isNearCapacity ? '#cf1322' : '#52c41a',
                      }}
                    />
                    <Progress
                      percent={Math.min(100, report.currentUtilizationPercentage)}
                      strokeColor={report.isNearCapacity ? '#cf1322' : '#52c41a'}
                      format={() =>
                        `${report.currentUtilizationPercentage}% / ${report.maxDailyCapacity}`
                      }
                    />
                  </Col>
                  <Col span={6}>
                    <Statistic
                      title={t('tseCapacity.peakHourly')}
                      value={report.peakHourlyTransactions}
                      suffix={`/ ${report.maxHourlyCapacity}`}
                    />
                  </Col>
                  <Col span={6}>
                    <Statistic
                      title={t('tseCapacity.nextMonth')}
                      value={report.estimatedNextMonthTransactions}
                    />
                  </Col>
                </Row>
                <Space style={{ marginTop: 16 }} wrap>
                  <Tag color={report.isNearCapacity ? 'red' : 'green'}>
                    {report.isNearCapacity
                      ? t('tseCapacity.nearCapacity')
                      : t('tseCapacity.okCapacity')}
                  </Tag>
                  <Tag>
                    {t('tseCapacity.growth')}: {report.dailyGrowthRatePercent}%
                  </Tag>
                  <Tag>
                    {t('tseCapacity.devices')}: {report.activeSigningDevices}
                  </Tag>
                  <Tag>
                    {t('tseCapacity.reachDate')}:{' '}
                    {report.estimatedCapacityReachDate
                      ? dayjs(report.estimatedCapacityReachDate).format('YYYY-MM-DD')
                      : t('tseCapacity.reachUnknown')}
                  </Tag>
                </Space>
              </>
            ) : reportQuery.isError ? (
              <Alert type="error" showIcon message={t('tseCapacity.loadError')} />
            ) : null}
          </Card>

          <Card title={t('tseCapacity.trendsTitle')} loading={reportQuery.isLoading}>
            <Table
              rowKey={(row) => row.date}
              columns={trendColumns}
              dataSource={report?.dailyTrends ?? []}
              pagination={{ pageSize: 14 }}
              size="small"
            />
          </Card>

          <Card title={t('tseCapacity.forecastTitle')} loading={forecastQuery.isLoading}>
            {forecastQuery.data ? (
              <>
                <Space style={{ marginBottom: 12 }}>
                  <Tag>
                    {t('tseCapacity.confidence')}: {forecastQuery.data.confidence}
                  </Tag>
                  <Tag>
                    {t('tseCapacity.dailyAvg')}: {forecastQuery.data.estimatedDailyAverage}
                  </Tag>
                </Space>
                <Table
                  rowKey={(row) => row.date}
                  columns={forecastColumns}
                  dataSource={forecastQuery.data.dailyPoints}
                  pagination={{ pageSize: 10 }}
                  size="small"
                />
              </>
            ) : null}
          </Card>

          <Card title={t('tseCapacity.recommendationsTitle')}>
            <List
              dataSource={report?.recommendations ?? []}
              locale={{ emptyText: t('tseCapacity.noRecommendations') }}
              renderItem={(item) => <List.Item>{item}</List.Item>}
            />
          </Card>
        </Space>
      )}
    </>
  );
}
