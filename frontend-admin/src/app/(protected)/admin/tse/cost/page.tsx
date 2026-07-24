'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  List,
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
  getTseCostAnomalies,
  getTseCostReport,
} from '@/features/tse-failover/api/tse';
import type { TseCostReport, TseCostTrend } from '@/features/tse-failover/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const COST_KEY = ['admin', 'tse-cost'] as const;

export default function TseCostPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-cost'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const reportQuery = useQuery({
    queryKey: [...COST_KEY, 'report', tenantId],
    queryFn: ({ signal }) => getTseCostReport(tenantId!, 30, signal),
    enabled: allowed && !!tenantId,
  });

  const anomalyMutation = useMutation({
    mutationFn: () => getTseCostAnomalies(tenantId!),
    onSuccess: (alert) => {
      notify.success(t('tseCost.checkSuccess'));
      if (alert.hasAnomaly) {
        notify.warning(alert.message, { mode: 'notification' });
      }
      void reportQuery.refetch();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseCost.checkAnomalies',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const report: TseCostReport | undefined = reportQuery.data;

  const trendColumns: ColumnsType<TseCostTrend> = [
    {
      title: t('tseCost.colDate'),
      dataIndex: 'date',
      key: 'date',
      render: (d: string) => dayjs(d).format('YYYY-MM-DD'),
    },
    {
      title: t('tseCost.colTransactions'),
      dataIndex: 'transactionCount',
      key: 'transactionCount',
    },
    {
      title: t('tseCost.colCost'),
      dataIndex: 'estimatedCost',
      key: 'estimatedCost',
      render: (v: number) => `€${Number(v).toFixed(2)}`,
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseCost.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseCost.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseCost.title') }]}
        extra={
          <Space>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 260 }}
              placeholder={t('tseCost.tenantLabel')}
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
              onClick={() => void reportQuery.refetch()}
            >
              {t('tseCost.loadReport')}
            </Button>
            <Button
              type="primary"
              disabled={!tenantId}
              loading={anomalyMutation.isPending}
              onClick={() => anomalyMutation.mutate()}
            >
              {t('tseCost.checkAnomalies')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseCost.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseCost.emptySelect')} />
      ) : (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Card title={t('tseCost.cardTitle')} loading={reportQuery.isLoading}>
            {report ? (
              <>
                <Typography.Text type="secondary">{t('tseCost.indicativeNote')}</Typography.Text>
                {(anomalyMutation.data?.hasAnomaly || report.hasCostAnomaly) && (
                  <Alert
                    style={{ marginTop: 12 }}
                    type={anomalyMutation.data?.severity === 'Critical' ? 'error' : 'warning'}
                    showIcon
                    message={t('tseCost.anomalyTitle')}
                    description={
                      anomalyMutation.data?.message || report.anomalyDescription || undefined
                    }
                  />
                )}
                <Row gutter={16} style={{ marginTop: 16 }}>
                  <Col span={8}>
                    <Statistic
                      title={t('tseCost.totalCost')}
                      value={report.totalCost}
                      precision={2}
                      prefix="€"
                    />
                  </Col>
                  <Col span={8}>
                    <Statistic
                      title={t('tseCost.avgPerTx')}
                      value={report.averageCostPerTransaction}
                      precision={4}
                      prefix="€"
                    />
                  </Col>
                  <Col span={8}>
                    <Statistic
                      title={t('tseCost.potentialSavings')}
                      value={report.potentialSavings}
                      precision={2}
                      prefix="€"
                    />
                  </Col>
                </Row>
                <Row gutter={16} style={{ marginTop: 16 }}>
                  <Col span={8}>
                    <Statistic title={t('tseCost.transactions')} value={report.totalTransactions} />
                  </Col>
                  <Col span={8}>
                    <Statistic title={t('tseCost.activeDevices')} value={report.activeDeviceCount} />
                  </Col>
                  <Col span={8}>
                    <Statistic title={t('tseCost.backupDevices')} value={report.backupDeviceCount} />
                  </Col>
                </Row>
              </>
            ) : reportQuery.isError ? (
              <Alert type="error" showIcon message={t('tseCost.loadError')} />
            ) : null}
          </Card>

          <Card title={t('tseCost.recommendationsTitle')}>
            <List
              dataSource={report?.recommendations ?? []}
              locale={{ emptyText: t('tseCost.recommendationsEmpty') }}
              renderItem={(rec) => (
                <List.Item>
                  <List.Item.Meta
                    title={
                      <Space>
                        <Tag color={rec.severity === 'Critical' ? 'red' : 'orange'}>
                          {rec.severity}
                        </Tag>
                        {rec.title}
                      </Space>
                    }
                    description={
                      rec.estimatedMonthlySavings > 0
                        ? `${rec.description} (${t('tseCost.estMonthlySavings')}: €${rec.estimatedMonthlySavings.toFixed(2)})`
                        : rec.description
                    }
                  />
                </List.Item>
              )}
            />
          </Card>

          <Card title={t('tseCost.trendsTitle')}>
            <Table
              rowKey={(row) => row.date}
              columns={trendColumns}
              dataSource={report?.dailyTrends ?? []}
              pagination={{ pageSize: 14 }}
              size="small"
            />
          </Card>
        </Space>
      )}
    </>
  );
}
