'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useMemo, useState } from 'react';

import {
  endMaintenance,
  fetchAllMaintenanceNotifications,
  getMaintenanceStatus,
  startMaintenance,
  type MaintenanceNotificationDto,
  type StartMaintenanceModeRequest,
} from '@/api/manual/maintenanceMode';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { dateColumnRender } from '@/components/DateColumn';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

const STATUS_QUERY_KEY = ['admin', 'maintenance', 'status'] as const;
const LIST_QUERY_KEY = ['admin', 'maintenance', 'notifications'] as const;

export default function MaintenanceManagementPage() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { isSuperAdmin } = usePermissions();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<{
    title?: string;
    message?: string;
    scheduledEndAt?: dayjs.Dayjs;
  }>();
  const [startOpen, setStartOpen] = useState(false);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.administration'), href: '/admin' },
    { title: t('nav.maintenance') },
  ];

  const statusQuery = useQuery({
    queryKey: STATUS_QUERY_KEY,
    queryFn: ({ signal }) => getMaintenanceStatus(signal),
    enabled: isSuperAdmin,
    refetchInterval: 30_000,
  });

  const listQuery = useQuery({
    queryKey: LIST_QUERY_KEY,
    queryFn: () => fetchAllMaintenanceNotifications({ limit: 50 }),
    enabled: isSuperAdmin,
  });

  const startMutation = useMutation({
    mutationFn: (body: StartMaintenanceModeRequest) => startMaintenance(body),
    onSuccess: async () => {
      notify.successKey('maintenance.manage.started');
      setStartOpen(false);
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
      await queryClient.invalidateQueries({ queryKey: LIST_QUERY_KEY });
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'maintenance-notifications', 'active'],
      });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'MaintenancePage.start',
        fallbackKey: 'maintenance.manage.actionFailed',
      });
    },
  });

  const endMutation = useMutation({
    mutationFn: () => endMaintenance(),
    onSuccess: async () => {
      notify.successKey('maintenance.manage.ended');
      await queryClient.invalidateQueries({ queryKey: STATUS_QUERY_KEY });
      await queryClient.invalidateQueries({ queryKey: LIST_QUERY_KEY });
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'maintenance-notifications', 'active'],
      });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'MaintenancePage.end',
        fallbackKey: 'maintenance.manage.actionFailed',
      });
    },
  });

  const status = statusQuery.data;
  const items = listQuery.data?.items ?? [];

  const columns: ColumnsType<MaintenanceNotificationDto> = useMemo(
    () => [
      {
        title: t('maintenance.manage.columns.title'),
        dataIndex: 'title',
        key: 'title',
        ellipsis: true,
      },
      {
        title: t('maintenance.manage.columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 120,
        render: (value: string) => <Tag>{value}</Tag>,
      },
      {
        title: t('maintenance.manage.columns.start'),
        dataIndex: 'scheduledStartAt',
        key: 'scheduledStartAt',
        render: dateColumnRender('datetime'),
      },
      {
        title: t('maintenance.manage.columns.end'),
        dataIndex: 'scheduledEndAt',
        key: 'scheduledEndAt',
        render: dateColumnRender('datetime'),
      },
      {
        title: t('maintenance.manage.columns.priority'),
        dataIndex: 'priority',
        key: 'priority',
        width: 90,
      },
    ],
    [t],
  );

  if (!isSuperAdmin) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('maintenance.manage.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('maintenance.manage.accessDenied')} />
      </AdminPageShell>
    );
  }

  const confirmEnd = () => {
    modal.confirm({
      title: t('maintenance.manage.endConfirmTitle'),
      content: t('maintenance.manage.endConfirmBody'),
      okText: t('maintenance.manage.endAction'),
      onOk: () => endMutation.mutateAsync(),
    });
  };

  const submitStart = async () => {
    const values = await form.validateFields();
    await startMutation.mutateAsync({
      title: values.title,
      message: values.message,
      scheduledEndAt: values.scheduledEndAt
        ? values.scheduledEndAt.toISOString()
        : undefined,
      isMandatory: true,
      priority: 5,
    });
  };

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('maintenance.manage.pageTitle')}
        breadcrumbs={breadcrumbs}
      />

      <Card title={t('maintenance.manage.modeCardTitle')}>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap>
            <Tag color={status?.isActive ? 'error' : 'success'}>
              {status?.isActive
                ? t('maintenance.manage.statusActive')
                : t('maintenance.manage.statusInactive')}
            </Tag>
            {status?.isActive ? (
              <Typography.Text type="secondary">
                {t('maintenance.manage.blocksHint')}
              </Typography.Text>
            ) : null}
          </Space>

          {status?.isActive ? (
            <div>
              {status.startedAt ? (
                <Typography.Text type="secondary" style={{ display: 'block' }}>
                  {t('maintenance.starts')}:{' '}
                  {new Date(status.startedAt).toLocaleString(formatLocale)}
                </Typography.Text>
              ) : null}
              {status.scheduledEndAt ? (
                <Typography.Text type="secondary" style={{ display: 'block' }}>
                  {t('maintenance.ends')}:{' '}
                  {new Date(status.scheduledEndAt).toLocaleString(formatLocale)}
                </Typography.Text>
              ) : null}
              {status.message ? (
                <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
                  {status.message}
                </Typography.Paragraph>
              ) : null}
            </div>
          ) : null}

          <Space>
            {!status?.isActive ? (
              <Button type="primary" danger onClick={() => setStartOpen(true)}>
                {t('maintenance.manage.startAction')}
              </Button>
            ) : (
              <Button
                type="primary"
                onClick={confirmEnd}
                loading={endMutation.isPending}
              >
                {t('maintenance.manage.endAction')}
              </Button>
            )}
            <Button
              onClick={() => {
                void statusQuery.refetch();
                void listQuery.refetch();
              }}
            >
              {t('maintenance.manage.refresh')}
            </Button>
          </Space>
        </Space>
      </Card>

      <Card
        title={t('maintenance.manage.notificationsCardTitle')}
        style={{ marginTop: 16 }}
      >
        <Table<MaintenanceNotificationDto>
          rowKey="id"
          loading={listQuery.isLoading}
          columns={columns}
          dataSource={items}
          pagination={false}
          locale={{ emptyText: t('maintenance.manage.emptyNotifications') }}
        />
      </Card>

      {startOpen ? (
        <Card title={t('maintenance.manage.startModalTitle')} style={{ marginTop: 16 }}>
          <Form form={form} layout="vertical" onFinish={() => void submitStart()}>
            <Form.Item name="title" label={t('maintenance.manage.formTitle')}>
              <Input maxLength={200} />
            </Form.Item>
            <Form.Item name="message" label={t('maintenance.manage.formMessage')}>
              <Input.TextArea rows={3} maxLength={4000} />
            </Form.Item>
            <Form.Item
              name="scheduledEndAt"
              label={t('maintenance.manage.formEnd')}
              initialValue={dayjs().add(2, 'hour')}
            >
              <DatePicker showTime style={{ width: '100%' }} />
            </Form.Item>
            <Space>
              <Button
                type="primary"
                danger
                htmlType="submit"
                loading={startMutation.isPending}
              >
                {t('maintenance.manage.startAction')}
              </Button>
              <Button onClick={() => setStartOpen(false)}>
                {t('maintenance.dismiss')}
              </Button>
            </Space>
          </Form>
        </Card>
      ) : null}
    </AdminPageShell>
  );
}
