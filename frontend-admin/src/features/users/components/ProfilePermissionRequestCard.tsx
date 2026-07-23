'use client';

import { Button, Card, Form, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type PermissionRequestDto,
  createPermissionRequest,
  fetchMyPermissionRequests,
} from '@/features/users/api/permissionRequestsApi';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

type FormValues = {
  permission: string;
  reason: string;
  duration: '1d' | '7d' | '30d';
};

const FALLBACK_PERMISSIONS = [
  'report.view',
  'report.export',
  'audit.view',
  'settings.view',
  'backup.manage',
  'user.view',
  'product.manage',
  'customer.view',
];

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'approved') return 'success';
  if (s === 'rejected') return 'error';
  return 'processing';
}

export function ProfilePermissionRequestCard() {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const [form] = Form.useForm<FormValues>();
  const queryClient = useQueryClient();
  const catalogQuery = usePermissionsCatalog({ enabled: true });

  const mineQuery = useQuery({
    queryKey: ['permission-requests', 'mine'],
    queryFn: fetchMyPermissionRequests,
  });

  const createMutation = useMutation({
    mutationFn: (values: FormValues) =>
      createPermissionRequest({
        permission: values.permission,
        reason: values.reason.trim(),
        duration: values.duration,
      }),
    onSuccess: () => {
      message.success(t('users.permissionRequests.submitSuccess'));
      form.resetFields();
      void queryClient.invalidateQueries({ queryKey: ['permission-requests', 'mine'] });
    },
    onError: () => message.error(t('users.permissionRequests.submitError')),
  });

  const permissionOptions = useMemo(() => {
    const catalog = catalogQuery.data ?? [];
    const keys = catalog.length > 0 ? catalog.map((c) => c.key) : FALLBACK_PERMISSIONS;
    return keys.map((key) => ({
      value: key,
      label: `${resolvePermissionDisplayLabel(key, t)} (${key})`,
    }));
  }, [catalogQuery.data, t]);

  const columns: ColumnsType<PermissionRequestDto> = [
    {
      title: t('users.permissionRequests.columnPermission'),
      dataIndex: 'permission',
      render: (perm: string) => resolvePermissionDisplayLabel(perm, t),
    },
    {
      title: t('users.permissionRequests.columnDuration'),
      dataIndex: 'requestedDuration',
      width: 90,
    },
    {
      title: t('users.permissionRequests.columnStatus'),
      dataIndex: 'status',
      width: 120,
      render: (status: string) => <Tag color={statusColor(status)}>{status}</Tag>,
    },
    {
      title: t('users.permissionRequests.columnRequestedAt'),
      dataIndex: 'requestedAt',
      width: 160,
      render: (iso: string) => formatDateTime(iso, formatLocale),
    },
    {
      title: t('users.permissionRequests.columnReason'),
      dataIndex: 'reason',
      ellipsis: true,
    },
  ];

  return (
    <Card title={t('users.permissionRequests.cardTitle')} variant="borderless">
      <Typography.Paragraph type="secondary">{t('users.permissionRequests.intro')}</Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        initialValues={{ duration: '7d' }}
        onFinish={(values) => createMutation.mutate(values)}
        style={{ maxWidth: 560 }}
      >
        <Form.Item
          name="permission"
          label={t('users.permissionRequests.permissionLabel')}
          rules={[{ required: true, message: t('users.permissionRequests.permissionRequired') }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={permissionOptions}
            placeholder={t('users.permissionRequests.permissionPlaceholder')}
          />
        </Form.Item>
        <Form.Item
          name="reason"
          label={t('users.permissionRequests.reasonLabel')}
          rules={[{ required: true, message: t('users.permissionRequests.reasonRequired') }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
        <Form.Item
          name="duration"
          label={t('users.permissionRequests.durationLabel')}
          rules={[{ required: true }]}
        >
          <Select
            options={[
              { value: '1d', label: t('users.permissionRequests.duration1d') },
              { value: '7d', label: t('users.permissionRequests.duration7d') },
              { value: '30d', label: t('users.permissionRequests.duration30d') },
            ]}
          />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={createMutation.isPending}>
            {t('users.permissionRequests.submit')}
          </Button>
        </Form.Item>
      </Form>

      <Typography.Title level={5} style={{ marginTop: 16 }}>
        {t('users.permissionRequests.historyTitle')}
      </Typography.Title>
      <Table
        rowKey="id"
        size="small"
        loading={mineQuery.isLoading}
        dataSource={mineQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 5 }}
        locale={{ emptyText: t('users.permissionRequests.emptyHistory') }}
      />
    </Card>
  );
}
