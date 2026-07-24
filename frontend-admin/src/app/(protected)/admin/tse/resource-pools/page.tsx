'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Form,
  Input,
  InputNumber,
  List,
  Modal,
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
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  assignTenantToTsePool,
  createTseResourcePool,
  getTsePoolMetrics,
  getTsePoolStatus,
  listTseResourcePools,
  unassignTenantFromTsePool,
} from '@/features/tse-resource-pools/api/pools';
import type {
  AssignTenantToTsePoolRequest,
  CreateTseResourcePoolRequest,
  TseResourcePool,
} from '@/features/tse-resource-pools/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const POOLS_KEY = ['admin', 'tse-resource-pools'] as const;

export default function TseResourcePoolsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();

  const [createOpen, setCreateOpen] = useState(false);
  const [assignPoolId, setAssignPoolId] = useState<string | null>(null);
  const [detailsPoolId, setDetailsPoolId] = useState<string | null>(null);
  const [createForm] = Form.useForm<{
    name: string;
    type: string;
    totalCapacity: number;
    description?: string;
  }>();
  const [assignForm] = Form.useForm<{ tenantId: string; reservedCapacity: number }>();

  const poolsQuery = useQuery({
    queryKey: POOLS_KEY,
    queryFn: ({ signal }) => listTseResourcePools(signal),
    enabled: allowed,
  });

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-pools'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed && !!assignPoolId,
    staleTime: 60_000,
  });

  const detailsPool = useMemo(
    () => poolsQuery.data?.find((p) => p.id === detailsPoolId) ?? null,
    [poolsQuery.data, detailsPoolId]
  );

  const statusQuery = useQuery({
    queryKey: [...POOLS_KEY, 'status', detailsPoolId],
    queryFn: ({ signal }) => getTsePoolStatus(detailsPoolId!, signal),
    enabled: allowed && !!detailsPoolId,
  });

  const metricsQuery = useQuery({
    queryKey: [...POOLS_KEY, 'metrics', detailsPoolId],
    queryFn: ({ signal }) => getTsePoolMetrics(detailsPoolId!, signal),
    enabled: allowed && !!detailsPoolId,
  });

  const createMutation = useMutation({
    mutationFn: (body: CreateTseResourcePoolRequest) => createTseResourcePool(body),
    onSuccess: async () => {
      notify.success(t('tseResourcePools.createSuccess'));
      setCreateOpen(false);
      createForm.resetFields();
      await queryClient.invalidateQueries({ queryKey: POOLS_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseResourcePools.create',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const assignMutation = useMutation({
    mutationFn: (body: AssignTenantToTsePoolRequest) => assignTenantToTsePool(body),
    onSuccess: async () => {
      notify.success(t('tseResourcePools.assignSuccess'));
      setAssignPoolId(null);
      assignForm.resetFields();
      await queryClient.invalidateQueries({ queryKey: POOLS_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseResourcePools.assign',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const unassignMutation = useMutation({
    mutationFn: (tenantId: string) => unassignTenantFromTsePool(tenantId),
    onSuccess: async () => {
      notify.success(t('tseResourcePools.unassignSuccess'));
      await queryClient.invalidateQueries({ queryKey: POOLS_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseResourcePools.unassign',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const typeLabel = (type: string) => {
    switch (type) {
      case 'Dedicated':
        return t('tseResourcePools.typeDedicated');
      case 'Hybrid':
        return t('tseResourcePools.typeHybrid');
      default:
        return t('tseResourcePools.typeShared');
    }
  };

  const columns: ColumnsType<TseResourcePool> = [
    {
      title: t('tseResourcePools.colName'),
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: t('tseResourcePools.colType'),
      dataIndex: 'type',
      key: 'type',
      render: (type: string) => <Tag>{typeLabel(type)}</Tag>,
    },
    {
      title: t('tseResourcePools.colCapacity'),
      key: 'capacity',
      render: (_, record) => {
        const percent =
          record.totalCapacity <= 0
            ? 0
            : Math.round((record.usedCapacity / record.totalCapacity) * 100);
        return (
          <Progress
            percent={percent}
            size="small"
            status={percent >= 90 ? 'exception' : percent >= 75 ? 'active' : 'normal'}
            format={() => `${record.usedCapacity}/${record.totalCapacity}`}
          />
        );
      },
    },
    {
      title: t('tseResourcePools.colTenants'),
      key: 'assignedTenants',
      render: (_, record) => record.assignedTenants?.length ?? 0,
    },
    {
      title: t('tseResourcePools.colActive'),
      dataIndex: 'isActive',
      key: 'isActive',
      render: (active: boolean) => (
        <Tag color={active ? 'success' : 'default'}>{active ? 'Yes' : 'No'}</Tag>
      ),
    },
    {
      title: t('tseResourcePools.colActions'),
      key: 'actions',
      render: (_, record) => (
        <Space wrap>
          <Button size="small" onClick={() => setDetailsPoolId(record.id)}>
            {t('tseResourcePools.actionDetails')}
          </Button>
          <Button size="small" onClick={() => setAssignPoolId(record.id)}>
            {t('tseResourcePools.actionAssign')}
          </Button>
        </Space>
      ),
    },
  ];

  if (!allowed) {
    return (
      <Alert type="error" showIcon message={t('tseResourcePools.forbidden')} />
    );
  }

  const tenantOptions =
    (tenantsQuery.data ?? []).map((tenant) => ({
      value: tenant.id,
      label: tenant.name || tenant.slug || tenant.id,
    }));

  return (
    <>
      <AdminPageHeader
        title={t('tseResourcePools.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseResourcePools.title') }]}
        actions={
          <Button type="primary" onClick={() => setCreateOpen(true)}>
            {t('tseResourcePools.createButton')}
          </Button>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseResourcePools.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card>
        <Table<TseResourcePool>
          rowKey="id"
          loading={poolsQuery.isLoading}
          dataSource={poolsQuery.data ?? []}
          columns={columns}
          locale={{ emptyText: t('tseResourcePools.empty') }}
          pagination={{ pageSize: 20, showSizeChanger: true }}
        />
      </Card>

      <Modal
        title={t('tseResourcePools.createModalTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        onOk={() => createForm.submit()}
        confirmLoading={createMutation.isPending}
        destroyOnHidden
      >
        <Form
          form={createForm}
          layout="vertical"
          initialValues={{ type: 'Shared', totalCapacity: 10 }}
          onFinish={(values) => createMutation.mutate(values)}
        >
          <Form.Item
            name="name"
            label={t('tseResourcePools.nameLabel')}
            rules={[{ required: true, message: t('tseResourcePools.nameRequired') }]}
          >
            <Input maxLength={120} />
          </Form.Item>
          <Form.Item name="type" label={t('tseResourcePools.typeLabel')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'Shared', label: t('tseResourcePools.typeShared') },
                { value: 'Dedicated', label: t('tseResourcePools.typeDedicated') },
                { value: 'Hybrid', label: t('tseResourcePools.typeHybrid') },
              ]}
            />
          </Form.Item>
          <Form.Item
            name="totalCapacity"
            label={t('tseResourcePools.capacityLabel')}
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={100000} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="description" label={t('tseResourcePools.descriptionLabel')}>
            <Input.TextArea rows={3} maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseResourcePools.assignModalTitle')}
        open={!!assignPoolId}
        onCancel={() => setAssignPoolId(null)}
        onOk={() => assignForm.submit()}
        confirmLoading={assignMutation.isPending}
        destroyOnHidden
      >
        <Form
          form={assignForm}
          layout="vertical"
          initialValues={{ reservedCapacity: 1 }}
          onFinish={(values) =>
            assignMutation.mutate({
              tenantId: values.tenantId,
              poolId: assignPoolId!,
              reservedCapacity: values.reservedCapacity,
            })
          }
        >
          <Form.Item
            name="tenantId"
            label={t('tseResourcePools.tenantLabel')}
            rules={[{ required: true, message: t('tseResourcePools.tenantRequired') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              options={tenantOptions}
              loading={tenantsQuery.isLoading}
            />
          </Form.Item>
          <Form.Item
            name="reservedCapacity"
            label={t('tseResourcePools.reservedCapacityLabel')}
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={10000} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseResourcePools.detailsTitle')}
        open={!!detailsPoolId}
        onCancel={() => setDetailsPoolId(null)}
        footer={null}
        width={720}
        destroyOnHidden
      >
        {detailsPool ? (
          <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <Typography.Text>
              <strong>{detailsPool.name}</strong> · {typeLabel(detailsPool.type)}
            </Typography.Text>

            {(statusQuery.data?.warnings?.length ?? 0) > 0 ? (
              <Alert
                type="warning"
                showIcon
                message={t('tseResourcePools.warningsTitle')}
                description={
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {statusQuery.data?.warnings.map((w) => (
                      <li key={w}>{w}</li>
                    ))}
                  </ul>
                }
              />
            ) : null}

            <Card size="small" title={t('tseResourcePools.metricsTitle')}>
              <Row gutter={[16, 16]}>
                <Col xs={12} sm={8}>
                  <Statistic
                    title={t('tseResourcePools.statUtilization')}
                    value={metricsQuery.data?.utilizationPercent ?? statusQuery.data?.utilizationPercent ?? 0}
                    suffix="%"
                    precision={1}
                  />
                </Col>
                <Col xs={12} sm={8}>
                  <Statistic
                    title={t('tseResourcePools.statDevices')}
                    value={metricsQuery.data?.activeDeviceCount ?? 0}
                  />
                </Col>
                <Col xs={12} sm={8}>
                  <Statistic
                    title={t('tseResourcePools.statHealthy')}
                    value={metricsQuery.data?.healthyDeviceCount ?? 0}
                  />
                </Col>
                <Col xs={12} sm={8}>
                  <Statistic
                    title={t('tseResourcePools.statSigned')}
                    value={metricsQuery.data?.signedTransactionsLast30Days ?? 0}
                  />
                </Col>
                <Col xs={12} sm={8}>
                  <Statistic
                    title={t('tseResourcePools.statAvgHealth')}
                    value={metricsQuery.data?.averageHealthScore ?? 0}
                    precision={1}
                  />
                </Col>
              </Row>
            </Card>

            <Card size="small" title={t('tseResourcePools.tenantsTitle')}>
              <List
                size="small"
                dataSource={detailsPool.tenantSummaries ?? []}
                locale={{ emptyText: '—' }}
                renderItem={(item) => (
                  <List.Item
                    actions={[
                      <Button
                        key="unassign"
                        size="small"
                        danger
                        loading={unassignMutation.isPending}
                        onClick={() => unassignMutation.mutate(item.tenantId)}
                      >
                        {t('tseResourcePools.unassign')}
                      </Button>,
                    ]}
                  >
                    <Space direction="vertical" size={0}>
                      <Typography.Text>
                        {item.tenantName || item.tenantSlug || item.tenantId}
                      </Typography.Text>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        reserved={item.reservedCapacity}
                      </Typography.Text>
                    </Space>
                  </List.Item>
                )}
              />
            </Card>

            <Card size="small" title={t('tseResourcePools.rulesTitle')}>
              <List
                size="small"
                dataSource={detailsPool.rules ?? []}
                locale={{ emptyText: '—' }}
                renderItem={(rule) => (
                  <List.Item>
                    <Space>
                      <Tag>{rule.ruleType}</Tag>
                      <Typography.Text>{rule.ruleValue}</Typography.Text>
                      {rule.description ? (
                        <Typography.Text type="secondary">{rule.description}</Typography.Text>
                      ) : null}
                    </Space>
                  </List.Item>
                )}
              />
            </Card>
          </Space>
        ) : null}
      </Modal>
    </>
  );
}
