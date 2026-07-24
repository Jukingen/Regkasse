'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  createTseWebhook,
  deleteTseWebhook,
  listTseWebhooks,
  testTseWebhook,
} from '@/features/tse-webhooks/api/webhooks';
import {
  TSE_WEBHOOK_EVENT_OPTIONS,
  type TseWebhookRegistration,
} from '@/features/tse-webhooks/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-webhooks'] as const;

type CreateForm = {
  url: string;
  events: string[];
  secret?: string;
};

export default function TseWebhooksPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();
  const [createOpen, setCreateOpen] = useState(false);
  const [form] = Form.useForm<CreateForm>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-webhooks'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const listQuery = useQuery({
    queryKey: [...KEY, 'list', tenantId],
    queryFn: ({ signal }) => listTseWebhooks(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const createMutation = useMutation({
    mutationFn: (values: CreateForm) =>
      createTseWebhook({
        tenantId: tenantId!,
        url: values.url.trim(),
        events: values.events,
        secret: values.secret?.trim() || undefined,
      }),
    onSuccess: async () => {
      notify.success(t('tseWebhooks.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseWebhooks.create',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const testMutation = useMutation({
    mutationFn: (id: string) => testTseWebhook(id),
    onSuccess: async (result) => {
      if (result.success) {
        notify.success(t('tseWebhooks.testSuccess'), {
          mode: 'notification',
          description: result.message ?? undefined,
        });
      } else {
        notify.warning(t('tseWebhooks.testFailed'), {
          mode: 'notification',
          description: result.message ?? undefined,
        });
      }
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseWebhooks.test',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteTseWebhook(id),
    onSuccess: async () => {
      notify.success(t('tseWebhooks.deleteSuccess'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseWebhooks.delete',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const columns: ColumnsType<TseWebhookRegistration> = [
    {
      title: t('tseWebhooks.colUrl'),
      dataIndex: 'url',
      key: 'url',
      ellipsis: true,
    },
    {
      title: t('tseWebhooks.colEvents'),
      dataIndex: 'events',
      key: 'events',
      render: (events: string[]) => (events ?? []).join(', '),
    },
    {
      title: t('tseWebhooks.colStatus'),
      dataIndex: 'status',
      key: 'status',
      render: (s: string) => (
        <Tag color={s === 'Active' ? 'green' : s === 'Failing' ? 'red' : 'default'}>{s}</Tag>
      ),
    },
    {
      title: t('tseWebhooks.colLastDelivery'),
      dataIndex: 'lastDeliveryAt',
      key: 'lastDeliveryAt',
      render: (v: string | null | undefined) =>
        v ? dayjs(v).format('DD.MM.YYYY HH:mm') : '—',
    },
    {
      title: t('tseWebhooks.colActions'),
      key: 'actions',
      render: (_, record) => (
        <Space>
          <Button
            size="small"
            loading={testMutation.isPending}
            onClick={() => testMutation.mutate(record.id)}
          >
            {t('tseWebhooks.test')}
          </Button>
          <Button
            size="small"
            danger
            loading={deleteMutation.isPending}
            onClick={() => {
              modal.confirm({
                title: t('tseWebhooks.deleteConfirmTitle'),
                content: t('tseWebhooks.deleteConfirmBody'),
                okButtonProps: { danger: true },
                onOk: () => deleteMutation.mutateAsync(record.id),
              });
            }}
          >
            {t('tseWebhooks.delete')}
          </Button>
        </Space>
      ),
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseWebhooks.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseWebhooks.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseWebhooks.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseWebhooks.tenantLabel')}
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
          {t('tseWebhooks.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseWebhooks.emptySelect')} />
      ) : listQuery.isError ? (
        <Alert type="error" showIcon message={t('tseWebhooks.loadError')} />
      ) : (
        <Card
          title={t('tseWebhooks.cardTitle')}
          loading={listQuery.isLoading}
          extra={
            <Button type="primary" onClick={() => setCreateOpen(true)}>
              {t('tseWebhooks.create')}
            </Button>
          }
        >
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('tseWebhooks.secretNote')}
          />
          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={listQuery.data ?? []}
            columns={columns}
          />
        </Card>
      )}

      <Modal
        open={createOpen}
        title={t('tseWebhooks.createTitle')}
        onCancel={() => setCreateOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={createMutation.isPending}
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => createMutation.mutate(values)}
          initialValues={{ events: ['DeviceHealthChanged'] }}
        >
          <Form.Item
            name="url"
            label={t('tseWebhooks.fieldUrl')}
            rules={[{ required: true, message: t('tseWebhooks.urlRequired') }]}
          >
            <Input placeholder="https://hooks.example.com/tse" />
          </Form.Item>
          <Form.Item
            name="events"
            label={t('tseWebhooks.fieldEvents')}
            rules={[{ required: true, message: t('tseWebhooks.eventsRequired') }]}
          >
            <Select
              mode="multiple"
              options={TSE_WEBHOOK_EVENT_OPTIONS.map((value) => ({ value, label: value }))}
            />
          </Form.Item>
          <Form.Item name="secret" label={t('tseWebhooks.fieldSecret')}>
            <Input.Password placeholder={t('tseWebhooks.secretPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
