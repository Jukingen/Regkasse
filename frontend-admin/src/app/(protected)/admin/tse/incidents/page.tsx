'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Badge,
  Button,
  Card,
  Descriptions,
  Form,
  Input,
  List,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  createTseIncident,
  getTseIncident,
  getTseIncidentDashboard,
  getTseIncidentReport,
  updateTseIncidentStatus,
} from '@/features/tse-incidents/api/incidents';
import type {
  CreateTseIncidentRequest,
  TseIncident,
  TseIncidentReport,
  TseIncidentSeverity,
} from '@/features/tse-incidents/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

dayjs.extend(relativeTime);

const INCIDENTS_KEY = ['admin', 'tse-incidents'] as const;

function severityColor(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'red';
    case 'High':
      return 'orange';
    case 'Medium':
      return 'gold';
    default:
      return 'green';
  }
}

function statusColor(status: string): string {
  switch (status) {
    case 'Open':
      return 'red';
    case 'Investigating':
      return 'orange';
    case 'Resolved':
      return 'green';
    default:
      return 'default';
  }
}

export default function TseIncidentsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();

  const [createOpen, setCreateOpen] = useState(false);
  const [detailsId, setDetailsId] = useState<string | null>(null);
  const [resolveId, setResolveId] = useState<string | null>(null);
  const [report, setReport] = useState<TseIncidentReport | null>(null);
  const [createForm] = Form.useForm<{
    tenantId: string;
    title: string;
    description: string;
    severity: TseIncidentSeverity;
  }>();
  const [resolveForm] = Form.useForm<{ resolution: string }>();

  const dashboardQuery = useQuery({
    queryKey: INCIDENTS_KEY,
    queryFn: ({ signal }) => getTseIncidentDashboard(undefined, signal),
    enabled: allowed,
  });

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-incidents'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed && createOpen,
    staleTime: 60_000,
  });

  const detailsQuery = useQuery({
    queryKey: [...INCIDENTS_KEY, 'detail', detailsId],
    queryFn: ({ signal }) => getTseIncident(detailsId!, signal),
    enabled: allowed && !!detailsId,
  });

  const createMutation = useMutation({
    mutationFn: (body: CreateTseIncidentRequest) => createTseIncident(body),
    onSuccess: async () => {
      notify.success(t('tseIncidents.createSuccess'));
      setCreateOpen(false);
      createForm.resetFields();
      await queryClient.invalidateQueries({ queryKey: INCIDENTS_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseIncidents.create',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const resolveMutation = useMutation({
    mutationFn: ({
      incidentId,
      resolution,
    }: {
      incidentId: string;
      resolution: string;
    }) =>
      updateTseIncidentStatus(incidentId, {
        status: 'Resolved',
        resolution,
      }),
    onSuccess: async () => {
      notify.success(t('tseIncidents.resolveSuccess'));
      setResolveId(null);
      resolveForm.resetFields();
      await queryClient.invalidateQueries({ queryKey: INCIDENTS_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseIncidents.resolve',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const reportMutation = useMutation({
    mutationFn: (incidentId: string) => getTseIncidentReport(incidentId),
    onSuccess: (data) => setReport(data),
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseIncidents.report',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const incidents = dashboardQuery.data?.incidents ?? [];

  const severityOptions = useMemo(
    () => [
      { value: 'Critical', label: t('tseIncidents.severityCritical') },
      { value: 'High', label: t('tseIncidents.severityHigh') },
      { value: 'Medium', label: t('tseIncidents.severityMedium') },
      { value: 'Low', label: t('tseIncidents.severityLow') },
    ],
    [t]
  );

  const columns: ColumnsType<TseIncident> = [
    {
      title: t('tseIncidents.colTitle'),
      dataIndex: 'title',
      key: 'title',
    },
    {
      title: t('tseIncidents.colTenant'),
      key: 'tenant',
      render: (_, row) => row.tenantName ?? row.tenantSlug ?? row.tenantId,
    },
    {
      title: t('tseIncidents.colSeverity'),
      dataIndex: 'severity',
      key: 'severity',
      render: (severity: string) => <Tag color={severityColor(severity)}>{severity}</Tag>,
    },
    {
      title: t('tseIncidents.colStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => <Tag color={statusColor(status)}>{status}</Tag>,
    },
    {
      title: t('tseIncidents.colDetected'),
      dataIndex: 'detectedAt',
      key: 'detectedAt',
      render: (date: string) => dayjs(date).fromNow(),
    },
    {
      title: t('tseIncidents.colActions'),
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Button size="small" onClick={() => setDetailsId(record.id)}>
            {t('tseIncidents.actionView')}
          </Button>
          {record.status !== 'Resolved' && record.status !== 'Closed' ? (
            <Button size="small" onClick={() => setResolveId(record.id)}>
              {t('tseIncidents.actionResolve')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseIncidents.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseIncidents.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseIncidents.title') }]}
        extra={
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            {t('tseIncidents.reportButton')}
          </Button>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseIncidents.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card title={t('tseIncidents.title')} loading={dashboardQuery.isLoading}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
          <Space size="large">
            <Badge count={dashboardQuery.data?.openCount ?? 0} color="red">
              <span style={{ paddingInline: 8 }}>{t('tseIncidents.badgeOpen')}</span>
            </Badge>
            <Badge count={dashboardQuery.data?.investigatingCount ?? 0} color="orange">
              <span style={{ paddingInline: 8 }}>{t('tseIncidents.badgeInvestigating')}</span>
            </Badge>
            <Badge count={dashboardQuery.data?.resolvedCount ?? 0} color="green">
              <span style={{ paddingInline: 8 }}>{t('tseIncidents.badgeResolved')}</span>
            </Badge>
          </Space>
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            {t('tseIncidents.reportButton')}
          </Button>
        </div>

        <Table
          rowKey="id"
          columns={columns}
          dataSource={incidents}
          pagination={{ pageSize: 20 }}
          locale={{ emptyText: t('tseIncidents.empty') }}
        />
      </Card>

      <Modal
        title={t('tseIncidents.createModalTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        destroyOnHidden
        confirmLoading={createMutation.isPending}
        onOk={() => createForm.submit()}
      >
        <Form
          form={createForm}
          layout="vertical"
          initialValues={{ severity: 'Medium' }}
          onFinish={(values) => createMutation.mutate(values)}
        >
          <Form.Item
            name="tenantId"
            label={t('tseIncidents.tenantLabel')}
            rules={[{ required: true, message: t('tseIncidents.tenantRequired') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              loading={tenantsQuery.isLoading}
              options={(tenantsQuery.data ?? []).map((tenant) => ({
                value: tenant.id,
                label: `${tenant.name} (${tenant.slug})`,
              }))}
            />
          </Form.Item>
          <Form.Item
            name="title"
            label={t('tseIncidents.titleLabel')}
            rules={[{ required: true, message: t('tseIncidents.titleRequired') }]}
          >
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item
            name="description"
            label={t('tseIncidents.descriptionLabel')}
            rules={[{ required: true, message: t('tseIncidents.descriptionRequired') }]}
          >
            <Input.TextArea rows={4} maxLength={4000} />
          </Form.Item>
          <Form.Item name="severity" label={t('tseIncidents.severityLabel')} rules={[{ required: true }]}>
            <Select options={severityOptions} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseIncidents.resolveModalTitle')}
        open={!!resolveId}
        onCancel={() => setResolveId(null)}
        destroyOnHidden
        confirmLoading={resolveMutation.isPending}
        onOk={() => resolveForm.submit()}
      >
        <Form
          form={resolveForm}
          layout="vertical"
          onFinish={(values) => {
            if (!resolveId) return;
            resolveMutation.mutate({ incidentId: resolveId, resolution: values.resolution });
          }}
        >
          <Form.Item
            name="resolution"
            label={t('tseIncidents.resolutionLabel')}
            rules={[{ required: true, message: t('tseIncidents.resolutionRequired') }]}
          >
            <Input.TextArea rows={4} maxLength={4000} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseIncidents.detailsTitle')}
        open={!!detailsId}
        onCancel={() => {
          setDetailsId(null);
          setReport(null);
        }}
        destroyOnHidden
        footer={
          <Space>
            <Button
              loading={reportMutation.isPending}
              onClick={() => detailsId && reportMutation.mutate(detailsId)}
            >
              {t('tseIncidents.loadReport')}
            </Button>
            <Button
              onClick={() => {
                setDetailsId(null);
                setReport(null);
              }}
            >
              {t('common.buttons.close')}
            </Button>
          </Space>
        }
        width={720}
      >
        {detailsQuery.data ? (
          <>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={t('tseIncidents.colTitle')}>
                {detailsQuery.data.title}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseIncidents.colSeverity')}>
                <Tag color={severityColor(detailsQuery.data.severity)}>
                  {detailsQuery.data.severity}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('tseIncidents.colStatus')}>
                <Tag color={statusColor(detailsQuery.data.status)}>{detailsQuery.data.status}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('tseIncidents.descriptionLabel')}>
                {detailsQuery.data.description}
              </Descriptions.Item>
              {detailsQuery.data.resolution ? (
                <Descriptions.Item label={t('tseIncidents.resolutionLabel')}>
                  {detailsQuery.data.resolution}
                </Descriptions.Item>
              ) : null}
            </Descriptions>

            <Typography.Title level={5} style={{ marginTop: 16 }}>
              {t('tseIncidents.timelineTitle')}
            </Typography.Title>
            <List
              size="small"
              dataSource={detailsQuery.data.logs}
              locale={{ emptyText: '—' }}
              renderItem={(item) => (
                <List.Item>
                  <List.Item.Meta
                    title={`${item.eventType} · ${dayjs(item.createdAt).fromNow()}`}
                    description={item.message}
                  />
                </List.Item>
              )}
            />

            <Typography.Title level={5}>{t('tseIncidents.actionsTitle')}</Typography.Title>
            <List
              size="small"
              dataSource={detailsQuery.data.actions}
              locale={{ emptyText: '—' }}
              renderItem={(item) => (
                <List.Item>
                  <List.Item.Meta
                    title={`${item.actionType}${item.isCompleted ? ' ✓' : ''}`}
                    description={item.description}
                  />
                </List.Item>
              )}
            />
          </>
        ) : null}

        {report ? (
          <>
            <Typography.Title level={5} style={{ marginTop: 16 }}>
              {t('tseIncidents.reportTitle')}
            </Typography.Title>
            <Alert type="info" showIcon message={report.summary} />
          </>
        ) : null}
      </Modal>
    </>
  );
}
