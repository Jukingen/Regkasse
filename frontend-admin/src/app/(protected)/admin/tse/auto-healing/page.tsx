'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Divider,
  Drawer,
  Form,
  InputNumber,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useEffect, useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  configureTseHealing,
  diagnoseAndHealTseDevice,
  getTseHealingConfiguration,
  getTseHealingHistory,
} from '@/features/tse-auto-healing/api/autoHealing';
import type { TseHealingHistoryItem, TseHealingRule } from '@/features/tse-auto-healing/types';
import { getTseDevices } from '@/features/tse-failover/api/tse';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-auto-healing'] as const;

type ConfigForm = {
  enabled: boolean;
  maxAutoHealAttempts: number;
  cooldownMinutes: number;
  notifyOnHeal: boolean;
  allowAutoFailover: boolean;
};

function statusColor(status: string): string {
  switch (status) {
    case 'Succeeded':
    case 'Enabled':
      return 'success';
    case 'Failed':
    case 'Disabled':
      return 'error';
    case 'Cooldown':
    case 'DiagnosedOnly':
      return 'warning';
    default:
      return 'default';
  }
}

export default function TseAutoHealingPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [deviceId, setDeviceId] = useState<string | undefined>();
  const [historyOpen, setHistoryOpen] = useState(false);
  const [form] = Form.useForm<ConfigForm>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-auto-healing'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const configQuery = useQuery({
    queryKey: [...KEY, 'config', tenantId],
    queryFn: ({ signal }) => getTseHealingConfiguration(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const historyQuery = useQuery({
    queryKey: [...KEY, 'history', tenantId],
    queryFn: ({ signal }) => getTseHealingHistory(tenantId!, 50, signal),
    enabled: allowed && !!tenantId && historyOpen,
  });

  const devicesQuery = useQuery({
    queryKey: ['admin', 'tse-failover', 'devices', 'auto-healing'],
    queryFn: ({ signal }) => getTseDevices(signal),
    enabled: allowed && !!tenantId,
    staleTime: 30_000,
  });

  const tenantDevices = useMemo(() => {
    const all = devicesQuery.data ?? [];
    if (!tenantId) return [];
    return all.filter((d) => d.tenantId === tenantId);
  }, [devicesQuery.data, tenantId]);

  useEffect(() => {
    const cfg = configQuery.data;
    if (!cfg) return;
    form.setFieldsValue({
      enabled: cfg.enabled,
      maxAutoHealAttempts: cfg.maxAutoHealAttempts,
      cooldownMinutes: cfg.cooldownMinutes,
      notifyOnHeal: cfg.notifyOnHeal,
      allowAutoFailover: cfg.allowAutoFailover,
    });
  }, [configQuery.data, form]);

  useEffect(() => {
    setDeviceId(undefined);
  }, [tenantId]);

  const saveMutation = useMutation({
    mutationFn: (values: ConfigForm) => {
      const rules = (configQuery.data?.rules ?? []).map((r) => ({
        id: r.id,
        condition: r.condition,
        action: r.action,
        priority: r.priority,
        status: r.status,
      }));
      return configureTseHealing(tenantId!, { ...values, rules });
    },
    onSuccess: async () => {
      notify.success(t('tseAutoHealing.configSaved'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAutoHealing.saveConfig',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const diagnoseMutation = useMutation({
    mutationFn: () => diagnoseAndHealTseDevice(deviceId!),
    onSuccess: async (result) => {
      notify.success(t('tseAutoHealing.diagnoseSuccess'), {
        mode: 'notification',
        description: result.message,
      });
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseAutoHealing.diagnose',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const ruleColumns: ColumnsType<TseHealingRule> = [
    { title: t('tseAutoHealing.colCondition'), dataIndex: 'condition', key: 'condition' },
    { title: t('tseAutoHealing.colAction'), dataIndex: 'action', key: 'action' },
    { title: t('tseAutoHealing.colPriority'), dataIndex: 'priority', key: 'priority', width: 100 },
    {
      title: t('tseAutoHealing.colStatus'),
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag>,
    },
    {
      title: t('tseAutoHealing.colLastTriggered'),
      dataIndex: 'lastTriggeredAt',
      key: 'lastTriggeredAt',
      render: (v?: string | null) => (v ? dayjs(v).format('YYYY-MM-DD HH:mm') : '—'),
    },
  ];

  const historyColumns: ColumnsType<TseHealingHistoryItem> = [
    {
      title: t('tseAutoHealing.colTime'),
      dataIndex: 'startedAt',
      key: 'startedAt',
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm:ss'),
    },
    {
      title: t('tseAutoHealing.colDevice'),
      dataIndex: 'deviceId',
      key: 'deviceId',
      ellipsis: true,
    },
    { title: t('tseAutoHealing.colCondition'), dataIndex: 'condition', key: 'condition' },
    { title: t('tseAutoHealing.colAction'), dataIndex: 'action', key: 'action' },
    {
      title: t('tseAutoHealing.colStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag>,
    },
    {
      title: t('tseAutoHealing.colHealth'),
      key: 'health',
      render: (_, row) =>
        `${row.healthScoreBefore}${row.healthScoreAfter != null ? ` → ${row.healthScoreAfter}` : ''}`,
    },
    {
      title: t('tseAutoHealing.colMessage'),
      dataIndex: 'message',
      key: 'message',
      ellipsis: true,
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseAutoHealing.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseAutoHealing.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseAutoHealing.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseAutoHealing.tenantLabel')}
            value={tenantId}
            onChange={setTenantId}
            options={(tenantsQuery.data ?? []).map((ten) => ({
              value: ten.id,
              label: ten.name || ten.slug || ten.id,
            }))}
          />
        }
      />

      <Typography.Paragraph type="secondary">{t('tseAutoHealing.subtitle')}</Typography.Paragraph>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseAutoHealing.emptySelect')} />
      ) : configQuery.isError ? (
        <Alert type="error" showIcon message={t('tseAutoHealing.loadError')} />
      ) : (
        <Card title={t('tseAutoHealing.cardTitle')} loading={configQuery.isLoading}>
          <Alert type="info" showIcon message={t('tseAutoHealing.diagnosticNote')} style={{ marginBottom: 16 }} />

          <Form
            form={form}
            layout="vertical"
            onFinish={(values) => saveMutation.mutate(values)}
            initialValues={{
              enabled: false,
              maxAutoHealAttempts: 3,
              cooldownMinutes: 30,
              notifyOnHeal: true,
              allowAutoFailover: false,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
              <Space align="center" wrap>
                <Form.Item name="enabled" valuePropName="checked" noStyle>
                  <Switch
                    checkedChildren={t('tseAutoHealing.enabled')}
                    unCheckedChildren={t('tseAutoHealing.disabled')}
                  />
                </Form.Item>
                <Typography.Text type="secondary">{t('tseAutoHealing.enabledHint')}</Typography.Text>
              </Space>
              <Button onClick={() => setHistoryOpen(true)}>{t('tseAutoHealing.historyButton')}</Button>
            </div>

            <Divider />

            <Typography.Title level={5}>{t('tseAutoHealing.rulesTitle')}</Typography.Title>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={configQuery.data?.rules ?? []}
              columns={ruleColumns}
              style={{ marginBottom: 16 }}
            />

            <Row gutter={16}>
              <Col xs={24} md={8}>
                <Form.Item name="maxAutoHealAttempts" label={t('tseAutoHealing.maxAttempts')}>
                  <InputNumber min={1} max={20} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="cooldownMinutes" label={t('tseAutoHealing.cooldown')}>
                  <InputNumber min={1} max={1440} style={{ width: '100%' }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="notifyOnHeal" valuePropName="checked" label={t('tseAutoHealing.notifyOnHeal')}>
                  <Switch />
                </Form.Item>
              </Col>
            </Row>

            <Form.Item
              name="allowAutoFailover"
              valuePropName="checked"
              label={t('tseAutoHealing.allowAutoFailover')}
              extra={t('tseAutoHealing.allowAutoFailoverHint')}
            >
              <Switch />
            </Form.Item>

            <Space wrap style={{ marginBottom: 16 }}>
              <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
                {t('tseAutoHealing.saveConfig')}
              </Button>
            </Space>

            <Divider />

            <Space wrap align="end">
              <div>
                <Typography.Text type="secondary">{t('tseAutoHealing.deviceLabel')}</Typography.Text>
                <div>
                  <Select
                    showSearch
                    optionFilterProp="label"
                    style={{ minWidth: 280 }}
                    placeholder={t('tseAutoHealing.deviceLabel')}
                    value={deviceId}
                    onChange={setDeviceId}
                    options={tenantDevices.map((d) => ({
                      value: d.id,
                      label: `${d.serialNumber || d.id} (${d.healthStatus})`,
                    }))}
                  />
                </div>
              </div>
              <Button
                type="default"
                disabled={!deviceId}
                loading={diagnoseMutation.isPending}
                onClick={() => diagnoseMutation.mutate()}
              >
                {t('tseAutoHealing.runDiagnose')}
              </Button>
            </Space>
          </Form>
        </Card>
      )}

      <Drawer
        title={t('tseAutoHealing.historyTitle')}
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
        width={920}
      >
        <Table
          rowKey="id"
          size="small"
          loading={historyQuery.isLoading}
          dataSource={historyQuery.data?.items ?? []}
          columns={historyColumns}
          pagination={{ pageSize: 20 }}
        />
      </Drawer>
    </>
  );
}
