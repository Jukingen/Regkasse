'use client';

import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Divider,
  List,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  exportTseComplianceReport,
  getTseComplianceDashboard,
} from '@/features/tse-compliance/api/compliance';
import type {
  TseComplianceAuditTrailItem,
  TseComplianceCertificateRow,
} from '@/features/tse-compliance/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-compliance'] as const;

export default function TseCompliancePage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const [tenantId, setTenantId] = useState<string | undefined>();
  const [range, setRange] = useState<[Dayjs, Dayjs]>([
    dayjs().subtract(7, 'day'),
    dayjs(),
  ]);

  const fromUtc = range[0].startOf('day').toISOString();
  const toUtc = range[1].endOf('day').toISOString();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-compliance'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const dashboardQuery = useQuery({
    queryKey: [...KEY, 'dashboard', tenantId, fromUtc, toUtc],
    queryFn: ({ signal }) =>
      getTseComplianceDashboard(tenantId!, fromUtc, toUtc, signal),
    enabled: allowed && !!tenantId,
  });

  const exportMutation = useMutation({
    mutationFn: () => exportTseComplianceReport(tenantId!, fromUtc, toUtc),
    onSuccess: () => notify.success(t('tseCompliance.exportSuccess')),
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseCompliance.export',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const dashboard = dashboardQuery.data;
  const score = dashboard?.complianceScore ?? 0;

  const certColumns: ColumnsType<TseComplianceCertificateRow> = [
    { title: t('tseCompliance.colSerial'), dataIndex: 'serialNumber', key: 'serial' },
    { title: t('tseCompliance.colProvider'), dataIndex: 'provider', key: 'provider' },
    {
      title: t('tseCompliance.colCertStatus'),
      dataIndex: 'certificateStatus',
      key: 'cert',
      render: (v: string, row) => (
        <Tag color={row.isValid ? 'green' : 'red'}>{v}</Tag>
      ),
    },
    {
      title: t('tseCompliance.colLifecycle'),
      dataIndex: 'lifecycleStatus',
      key: 'life',
    },
    {
      title: t('tseCompliance.colExpires'),
      dataIndex: 'expiresAt',
      key: 'exp',
      render: (v?: string | null) => (v ? dayjs(v).format('YYYY-MM-DD') : '—'),
    },
    {
      title: t('tseCompliance.colHealth'),
      key: 'health',
      render: (_, row) => `${row.healthStatus} (${row.healthScore})`,
    },
  ];

  const auditColumns: ColumnsType<TseComplianceAuditTrailItem> = [
    {
      title: t('tseCompliance.colTime'),
      dataIndex: 'timestampUtc',
      key: 'time',
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm:ss'),
      width: 170,
    },
    { title: t('tseCompliance.colAction'), dataIndex: 'action', key: 'action' },
    { title: t('tseCompliance.colEntity'), dataIndex: 'entityType', key: 'entity' },
    {
      title: t('tseCompliance.colUser'),
      key: 'user',
      render: (_, row) => `${row.userId} (${row.userRole})`,
    },
    {
      title: t('tseCompliance.colStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (v: string) => <Tag>{v}</Tag>,
    },
    {
      title: t('tseCompliance.colDescription'),
      dataIndex: 'description',
      key: 'desc',
      ellipsis: true,
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseCompliance.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseCompliance.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseCompliance.title') }]}
        extra={
          <Space wrap>
            <Select
              showSearch
              optionFilterProp="label"
              style={{ minWidth: 240 }}
              placeholder={t('tseCompliance.tenantLabel')}
              loading={tenantsQuery.isLoading}
              value={tenantId}
              onChange={setTenantId}
              options={(tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
              }))}
            />
            <DatePicker.RangePicker
              value={range}
              onChange={(values) => {
                if (values?.[0] && values[1]) setRange([values[0], values[1]]);
              }}
            />
            <Button
              disabled={!tenantId}
              loading={dashboardQuery.isFetching}
              onClick={() => void dashboardQuery.refetch()}
            >
              {t('tseCompliance.refresh')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseCompliance.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseCompliance.emptySelect')} />
      ) : dashboardQuery.isError ? (
        <Alert type="error" showIcon message={t('tseCompliance.loadError')} />
      ) : (
        <Card title={t('tseCompliance.cardTitle')} loading={dashboardQuery.isLoading}>
          <Row gutter={16}>
            <Col xs={24} sm={12} md={6}>
              <Statistic
                title={t('tseCompliance.overall')}
                value={score}
                suffix="%"
                valueStyle={{ color: score > 90 ? '#52c41a' : '#faad14' }}
              />
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Statistic
                title={t('tseCompliance.signatureChain')}
                value={dashboard?.signatureChainStatus ?? '—'}
              />
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Statistic
                title={t('tseCompliance.certificatesValid')}
                value={`${dashboard?.validCertificates ?? 0}/${dashboard?.totalCertificates ?? 0}`}
              />
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Statistic
                title={t('tseCompliance.auditLogs')}
                value={dashboard?.auditLogCount ?? 0}
              />
            </Col>
          </Row>

          <Divider />

          <Tabs
            items={[
              {
                key: 'certificates',
                label: t('tseCompliance.tabCertificates'),
                children: (
                  <Table
                    rowKey="deviceId"
                    size="small"
                    columns={certColumns}
                    dataSource={dashboard?.certificates ?? []}
                    pagination={false}
                    locale={{ emptyText: t('tseCompliance.noCertificates') }}
                  />
                ),
              },
              {
                key: 'transactions',
                label: t('tseCompliance.tabTransactions'),
                children: (
                  <Space direction="vertical" size="large" style={{ width: '100%' }}>
                    <Row gutter={16}>
                      <Col span={6}>
                        <Statistic
                          title={t('tseCompliance.signed')}
                          value={dashboard?.transactions.signedTransactions ?? 0}
                        />
                      </Col>
                      <Col span={6}>
                        <Statistic
                          title={t('tseCompliance.unsigned')}
                          value={dashboard?.transactions.unsignedTransactions ?? 0}
                        />
                      </Col>
                      <Col span={6}>
                        <Statistic
                          title={t('tseCompliance.signedPercent')}
                          value={dashboard?.transactions.signedPercent ?? 0}
                          suffix="%"
                        />
                      </Col>
                      <Col span={6}>
                        <Statistic
                          title={t('tseCompliance.chainBreaks')}
                          value={dashboard?.transactions.chainBreakCount ?? 0}
                        />
                      </Col>
                    </Row>
                    <Row gutter={16}>
                      <Col span={8}>
                        <Statistic
                          title={t('tseCompliance.sequenceGaps')}
                          value={dashboard?.transactions.sequenceGapCount ?? 0}
                        />
                      </Col>
                      <Col span={8}>
                        <Statistic
                          title={t('tseCompliance.duplicates')}
                          value={dashboard?.transactions.duplicateCount ?? 0}
                        />
                      </Col>
                      <Col span={8}>
                        <Statistic
                          title={t('tseCompliance.missingSignatures')}
                          value={dashboard?.transactions.missingSignatureCount ?? 0}
                        />
                      </Col>
                    </Row>
                    <Typography.Title level={5}>{t('tseCompliance.issues')}</Typography.Title>
                    <List
                      size="small"
                      dataSource={dashboard?.transactions.issues ?? []}
                      locale={{ emptyText: '—' }}
                      renderItem={(item) => (
                        <List.Item>
                          <Space>
                            <Tag
                              color={
                                item.severity === 'Critical'
                                  ? 'red'
                                  : item.severity === 'Warning'
                                    ? 'orange'
                                    : 'blue'
                              }
                            >
                              {item.severity}
                            </Tag>
                            <Typography.Text>
                              {item.code}: {item.message}
                            </Typography.Text>
                          </Space>
                        </List.Item>
                      )}
                    />
                  </Space>
                ),
              },
              {
                key: 'audit',
                label: t('tseCompliance.tabAuditTrail'),
                children: (
                  <Table
                    rowKey="id"
                    size="small"
                    columns={auditColumns}
                    dataSource={dashboard?.auditTrail ?? []}
                    pagination={{ pageSize: 20 }}
                    locale={{ emptyText: t('tseCompliance.noAudit') }}
                  />
                ),
              },
              {
                key: 'export',
                label: t('tseCompliance.tabExport'),
                children: (
                  <Space direction="vertical" size="middle">
                    <Alert
                      type="info"
                      showIcon
                      message={t('tseCompliance.legalNotice')}
                      description={dashboard?.legalNoticeDe}
                    />
                    <Button
                      type="primary"
                      loading={exportMutation.isPending}
                      onClick={() => exportMutation.mutate()}
                    >
                      {t('tseCompliance.exportButton')}
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
