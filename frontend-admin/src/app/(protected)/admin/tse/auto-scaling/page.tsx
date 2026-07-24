'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Divider,
  Form,
  InputNumber,
  Row,
  Select,
  Space,
  Statistic,
  Switch,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useEffect, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  configureTseScalingPolicy,
  getTseScalingHistory,
  getTseScalingStatus,
  triggerTseScaling,
} from '@/features/tse-auto-scaling/api/autoScaling';
import type { TseScalingHistoryItem } from '@/features/tse-auto-scaling/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-auto-scaling'] as const;

type PolicyForm = {
  enabled: boolean;
  minDevices: number;
  maxDevices: number;
  targetTransactionsPerDevice: number;
  scaleUpThreshold: number;
  scaleDownThreshold: number;
  cooldownMinutes: number;
  autoProvision: boolean;
};

export default function TseAutoScalingPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [form] = Form.useForm<PolicyForm>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-auto-scaling'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const statusQuery = useQuery({
    queryKey: [...KEY, 'status', tenantId],
    queryFn: ({ signal }) => getTseScalingStatus(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const historyQuery = useQuery({
    queryKey: [...KEY, 'history', tenantId],
    queryFn: ({ signal }) => getTseScalingHistory(tenantId!, 50, signal),
    enabled: allowed && !!tenantId,
  });

  useEffect(() => {
    const policy = statusQuery.data?.policy;
    if (!policy) return;
    form.setFieldsValue({
      enabled: policy.enabled,
      minDevices: policy.minDevices,
      maxDevices: policy.maxDevices,
      targetTransactionsPerDevice: policy.targetTransactionsPerDevice,
      scaleUpThreshold: policy.scaleUpThreshold,
      scaleDownThreshold: policy.scaleDownThreshold,
      cooldownMinutes: policy.cooldownMinutes,
      autoProvision: policy.autoProvision,
    });
  }, [statusQuery.data?.policy, form]);

  const saveMutation = useMutation({
    mutationFn: (values: PolicyForm) => configureTseScalingPolicy(tenantId!, values),
    onSuccess: async () => {
      notify.success(t('tseAutoScaling.policySaved'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAutoScaling.savePolicy',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const triggerMutation = useMutation({
    mutationFn: () => triggerTseScaling(tenantId!),
    onSuccess: async (result) => {
      notify.success(t('tseAutoScaling.triggerSuccess'), {
        mode: 'notification',
        description: result.reason,
      });
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAutoScaling.trigger',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const status = statusQuery.data;
  const load = status?.currentLoadPercent ?? 0;

  const columns: ColumnsType<TseScalingHistoryItem> = [
    {
      title: t('tseAutoScaling.colTime'),
      dataIndex: 'timestamp',
      key: 'time',
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm:ss'),
    },
    { title: t('tseAutoScaling.colAction'), dataIndex: 'action', key: 'action' },
    { title: t('tseAutoScaling.colFrom'), dataIndex: 'from', key: 'from' },
    { title: t('tseAutoScaling.colTo'), dataIndex: 'to', key: 'to' },
    { title: t('tseAutoScaling.colReason'), dataIndex: 'reason', key: 'reason', ellipsis: true },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseAutoScaling.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAutoScaling.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseAutoScaling.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseAutoScaling.tenantLabel')}
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
          {t('tseAutoScaling.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseAutoScaling.emptySelect')} />
      ) : statusQuery.isError ? (
        <Alert type="error" showIcon message={t('tseAutoScaling.loadError')} />
      ) : (
        <Card title={t('tseAutoScaling.cardTitle')} loading={statusQuery.isLoading}>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseAutoScaling.simulationNote')}
          />

          <Form
            form={form}
            layout="vertical"
            onFinish={(values) => saveMutation.mutate(values)}
            initialValues={{ enabled: false, autoProvision: false }}
          >
            <Form.Item name="enabled" valuePropName="checked" label={t('tseAutoScaling.enabled')}>
              <Switch
                checkedChildren={t('tseAutoScaling.enabled')}
                unCheckedChildren={t('tseAutoScaling.disabled')}
              />
            </Form.Item>

            <Row gutter={16} style={{ marginBottom: 16 }}>
              <Col xs={24} md={8}>
                <Statistic
                  title={t('tseAutoScaling.currentDevices')}
                  value={status?.currentDevices ?? 0}
                />
              </Col>
              <Col xs={24} md={8}>
                <Statistic
                  title={t('tseAutoScaling.recommended')}
                  value={status?.recommendedDevices ?? 0}
                />
              </Col>
              <Col xs={24} md={8}>
                <Statistic
                  title={t('tseAutoScaling.load')}
                  value={load}
                  suffix="%"
                  valueStyle={{ color: load > 70 ? '#cf1322' : '#52c41a' }}
                />
              </Col>
            </Row>

            <Row gutter={16}>
              <Col xs={24} md={8}>
                <Form.Item name="minDevices" label={t('tseAutoScaling.minDevices')}>
                  <InputNumber min={1} max={50} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="maxDevices" label={t('tseAutoScaling.maxDevices')}>
                  <InputNumber min={1} max={50} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="targetTransactionsPerDevice" label={t('tseAutoScaling.targetPerDevice')}>
                  <InputNumber min={100} max={100000} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="scaleUpThreshold" label={t('tseAutoScaling.scaleUp')}>
                  <InputNumber min={50} max={99} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="scaleDownThreshold" label={t('tseAutoScaling.scaleDown')}>
                  <InputNumber min={5} max={80} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="cooldownMinutes" label={t('tseAutoScaling.cooldown')}>
                  <InputNumber min={5} max={1440} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
            </Row>

            <Form.Item
              name="autoProvision"
              valuePropName="checked"
              label={t('tseAutoScaling.autoProvision')}
              extra={t('tseAutoScaling.autoProvisionHint')}
            >
              <Switch />
            </Form.Item>

            <Space wrap>
              <Button type="default" htmlType="submit" loading={saveMutation.isPending}>
                {t('tseAutoScaling.savePolicy')}
              </Button>
              <Button
                type="primary"
                loading={triggerMutation.isPending}
                onClick={() => triggerMutation.mutate()}
              >
                {t('tseAutoScaling.trigger')}
              </Button>
            </Space>
          </Form>

          <Divider />

          <Typography.Title level={5}>{t('tseAutoScaling.historyTitle')}</Typography.Title>
          <Table
            rowKey="id"
            size="small"
            loading={historyQuery.isLoading}
            dataSource={historyQuery.data?.items ?? []}
            columns={columns}
            pagination={false}
          />
        </Card>
      )}
    </>
  );
}
