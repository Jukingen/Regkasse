'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Card,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  detectTseAnomalies,
  getTseAnomalyDashboard,
  resolveTseAnomaly,
} from '@/features/tse-anomaly-detection/api/anomalies';
import type { TseAnomaly, TseAnomalySeverity } from '@/features/tse-anomaly-detection/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-anomalies'] as const;

function severityColor(severity: TseAnomalySeverity): string {
  switch (severity) {
    case 'Critical':
      return 'red';
    case 'High':
      return 'orange';
    case 'Medium':
      return 'gold';
    case 'Low':
      return 'blue';
    default:
      return 'default';
  }
}

export default function TseAnomalyDetectionPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [detail, setDetail] = useState<TseAnomaly | null>(null);

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-anomalies'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const dashboardQuery = useQuery({
    queryKey: [...KEY, 'dashboard', tenantId],
    queryFn: ({ signal }) => getTseAnomalyDashboard(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const detectMutation = useMutation({
    mutationFn: () => detectTseAnomalies(tenantId!),
    onSuccess: async (result) => {
      notify.success(t('tseAnomalyDetection.detectSuccess'), {
        mode: 'notification',
        description: result.summary,
      });
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAnomalyDetection.detect',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const resolveMutation = useMutation({
    mutationFn: (id: string) => resolveTseAnomaly(id),
    onSuccess: async () => {
      notify.success(t('tseAnomalyDetection.resolveSuccess'));
      setDetail(null);
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAnomalyDetection.resolve',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const dashboard = dashboardQuery.data;
  const anomalies = dashboard?.anomalies ?? [];

  const columns: ColumnsType<TseAnomaly> = useMemo(
    () => [
      {
        title: t('tseAnomalyDetection.colTime'),
        dataIndex: 'detectedAt',
        key: 'detectedAt',
        render: (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm'),
      },
      {
        title: t('tseAnomalyDetection.colMetric'),
        dataIndex: 'metricName',
        key: 'metricName',
      },
      {
        title: t('tseAnomalyDetection.colCurrent'),
        dataIndex: 'currentValue',
        key: 'currentValue',
      },
      {
        title: t('tseAnomalyDetection.colExpected'),
        dataIndex: 'expectedValue',
        key: 'expectedValue',
      },
      {
        title: t('tseAnomalyDetection.colDeviation'),
        dataIndex: 'deviation',
        key: 'deviation',
        render: (deviation: number) => (
          <Typography.Text type={deviation > 20 ? 'danger' : 'warning'}>
            {deviation}%
          </Typography.Text>
        ),
      },
      {
        title: t('tseAnomalyDetection.colSeverity'),
        dataIndex: 'severity',
        key: 'severity',
        render: (severity: TseAnomalySeverity) => (
          <Tag color={severityColor(severity)}>{severity}</Tag>
        ),
      },
      {
        title: t('tseAnomalyDetection.colActions'),
        key: 'actions',
        render: (_, record) => (
          <Space>
            <Button size="small" onClick={() => setDetail(record)}>
              {t('tseAnomalyDetection.view')}
            </Button>
            {!record.isResolved && (
              <Button
                size="small"
                loading={resolveMutation.isPending}
                onClick={() => resolveMutation.mutate(record.id)}
              >
                {t('tseAnomalyDetection.resolve')}
              </Button>
            )}
          </Space>
        ),
      },
    ],
    [t, resolveMutation]
  );

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseAnomalyDetection.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAnomalyDetection.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseAnomalyDetection.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseAnomalyDetection.tenantLabel')}
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
          {t('tseAnomalyDetection.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseAnomalyDetection.emptySelect')} />
      ) : dashboardQuery.isError ? (
        <Alert type="error" showIcon message={t('tseAnomalyDetection.loadError')} />
      ) : (
        <Card title={t('tseAnomalyDetection.cardTitle')} loading={dashboardQuery.isLoading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseAnomalyDetection.diagnosticNote')}
          />

          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: 16,
              gap: 16,
              flexWrap: 'wrap',
            }}
          >
            <Space size="large" wrap>
              <Badge count={dashboard?.criticalCount ?? 0} color="red" overflowCount={999}>
                <Tag color="red">{t('tseAnomalyDetection.critical')}</Tag>
              </Badge>
              <Badge count={dashboard?.highCount ?? 0} color="orange" overflowCount={999}>
                <Tag color="orange">{t('tseAnomalyDetection.high')}</Tag>
              </Badge>
              <Badge count={dashboard?.mediumCount ?? 0} color="gold" overflowCount={999}>
                <Tag color="gold">{t('tseAnomalyDetection.medium')}</Tag>
              </Badge>
              <Badge count={dashboard?.lowCount ?? 0} color="blue" overflowCount={999}>
                <Tag color="blue">{t('tseAnomalyDetection.low')}</Tag>
              </Badge>
            </Space>
            <Button
              type="primary"
              loading={detectMutation.isPending}
              onClick={() => detectMutation.mutate()}
            >
              {t('tseAnomalyDetection.detectNow')}
            </Button>
          </div>

          <Table
            rowKey="id"
            size="small"
            pagination={{ pageSize: 20 }}
            dataSource={anomalies}
            columns={columns}
          />
        </Card>
      )}

      <Modal
        open={!!detail}
        title={t('tseAnomalyDetection.detailTitle')}
        onCancel={() => setDetail(null)}
        footer={
          <Space>
            <Button onClick={() => setDetail(null)}>{t('tseAnomalyDetection.close')}</Button>
            {detail && !detail.isResolved && (
              <Button
                type="primary"
                loading={resolveMutation.isPending}
                onClick={() => resolveMutation.mutate(detail.id)}
              >
                {t('tseAnomalyDetection.resolve')}
              </Button>
            )}
          </Space>
        }
        destroyOnHidden
      >
        {detail && (
          <Space direction="vertical" size="small" style={{ width: '100%' }}>
            <Typography.Text>
              <strong>{t('tseAnomalyDetection.colMetric')}:</strong> {detail.metricName}
            </Typography.Text>
            <Typography.Text>
              <strong>{t('tseAnomalyDetection.colSeverity')}:</strong>{' '}
              <Tag color={severityColor(detail.severity)}>{detail.severity}</Tag>
            </Typography.Text>
            <Typography.Paragraph>{detail.description}</Typography.Paragraph>
            {detail.suggestedAction && (
              <Typography.Paragraph type="secondary">
                {t('tseAnomalyDetection.suggestedAction')}: {detail.suggestedAction}
              </Typography.Paragraph>
            )}
          </Space>
        )}
      </Modal>
    </>
  );
}
