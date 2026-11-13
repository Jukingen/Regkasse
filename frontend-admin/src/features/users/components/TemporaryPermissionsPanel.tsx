'use client';

import { PlusOutlined } from '@ant-design/icons';
import {
  Button,
  DatePicker,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type UserPermissionOverrideDto,
  deleteUserPermissionOverride,
  listUserPermissionOverrides,
  upsertUserPermissionOverride,
  userEffectivePermissionsQueryKey,
  userPermissionOverridesQueryKey,
} from '@/features/users/api/userPermissionOverridesApi';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import {
  computePermissionOverrideStatus,
  permissionOverrideStatusColor,
  type PermissionOverrideStatus,
} from '@/features/users/utils/permissionOverrideStatus';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';

type TemporaryPermissionsPanelProps = {
  userId: string;
  readOnly?: boolean;
};

type AddFormValues = {
  permission: string;
  reason?: string;
  isGranted: boolean;
  validFrom?: Dayjs | null;
  expiresAt?: Dayjs | null;
};

export function TemporaryPermissionsPanel({
  userId,
  readOnly = false,
}: TemporaryPermissionsPanelProps) {
  const { t, formatLocale } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [includeExpired, setIncludeExpired] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [form] = Form.useForm<AddFormValues>();
  const catalogQuery = usePermissionsCatalog({ enabled: true });

  const overridesQuery = useQuery({
    queryKey: userPermissionOverridesQueryKey(userId, includeExpired),
    queryFn: () => listUserPermissionOverrides(userId, { includeExpired }),
    enabled: Boolean(userId),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: userPermissionOverridesQueryKey(userId, true) });
    void queryClient.invalidateQueries({ queryKey: userPermissionOverridesQueryKey(userId, false) });
    void queryClient.invalidateQueries({ queryKey: userEffectivePermissionsQueryKey(userId) });
  };

  const upsertMutation = useMutation({
    mutationFn: (body: {
      permission: string;
      isGranted: boolean;
      reason?: string | null;
      validFrom?: string | null;
      expiresAt?: string | null;
    }) => upsertUserPermissionOverride(userId, body),
    onSuccess: () => {
      invalidate();
      setAddOpen(false);
      form.resetFields();
      message.success(t('users.temporaryPermissions.saveSuccess'));
    },
    onError: () => message.error(t('users.temporaryPermissions.saveError')),
  });

  const deleteMutation = useMutation({
    mutationFn: (overrideId: string) => deleteUserPermissionOverride(userId, overrideId),
    onSuccess: () => {
      invalidate();
      message.success(t('users.temporaryPermissions.removeSuccess'));
    },
    onError: () => message.error(t('users.temporaryPermissions.removeError')),
  });

  const rows = useMemo(() => {
    const all = overridesQuery.data ?? [];
    return all.filter((row) => row.expiresAt != null && String(row.expiresAt).trim() !== '');
  }, [overridesQuery.data]);

  const permissionOptions = useMemo(
    () =>
      (catalogQuery.data ?? []).map((item) => ({
        value: item.key,
        label: `${resolvePermissionDisplayLabel(item.key, t)} (${item.key})`,
      })),
    [catalogQuery.data, t]
  );

  const statusLabel = (status: PermissionOverrideStatus) =>
    t(`users.temporaryPermissions.status.${status}`);

  const columns: ColumnsType<UserPermissionOverrideDto> = [
    {
      title: t('users.temporaryPermissions.columnPermission'),
      dataIndex: 'permission',
      render: (perm: string) => resolvePermissionDisplayLabel(perm, t),
    },
    {
      title: t('users.temporaryPermissions.columnGrant'),
      dataIndex: 'isGranted',
      width: 110,
      render: (granted: boolean) =>
        granted
          ? t('users.permissionsModal.statusGranted')
          : t('users.permissionsModal.statusDenied'),
    },
    {
      title: t('users.temporaryPermissions.columnValidFrom'),
      dataIndex: 'validFrom',
      width: 150,
      render: (d: string | null | undefined) =>
        d ? formatDateTime(d, formatLocale) : '—',
    },
    {
      title: t('users.temporaryPermissions.columnExpiresAt'),
      dataIndex: 'expiresAt',
      width: 150,
      render: (d: string | null | undefined, row) => {
        const status = (row.status as PermissionOverrideStatus | undefined) ??
          computePermissionOverrideStatus(row.validFrom, row.expiresAt);
        return (
          <Space size={4} wrap>
            <span>{d ? formatDateTime(d, formatLocale) : '—'}</span>
            <Tag color={permissionOverrideStatusColor(status)}>{statusLabel(status)}</Tag>
          </Space>
        );
      },
    },
    {
      title: t('users.temporaryPermissions.columnReason'),
      dataIndex: 'reason',
      ellipsis: true,
      render: (v: string | null | undefined) => v?.trim() || '—',
    },
    {
      key: 'actions',
      width: 90,
      render: (_: unknown, record) =>
        readOnly ? null : (
          <Button
            type="link"
            danger
            size="small"
            loading={deleteMutation.isPending}
            onClick={() => {
              modal.confirm({
                title: t('users.temporaryPermissions.removeConfirmTitle'),
                content: t('users.temporaryPermissions.removeConfirmBody'),
                onOk: () => deleteMutation.mutateAsync(record.id),
              });
            }}
          >
            {t('common.buttons.delete')}
          </Button>
        ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <Typography.Text type="secondary">{t('users.temporaryPermissions.intro')}</Typography.Text>
        <Space wrap>
          <Space size={6}>
            <Switch checked={includeExpired} onChange={setIncludeExpired} size="small" />
            <Typography.Text>{t('users.temporaryPermissions.includeExpired')}</Typography.Text>
          </Space>
          {!readOnly ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
              {t('users.temporaryPermissions.add')}
            </Button>
          ) : null}
        </Space>
      </div>

      <Table
        rowKey="id"
        size="small"
        loading={overridesQuery.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={{ pageSize: 8 }}
        locale={{ emptyText: t('users.temporaryPermissions.empty') }}
      />

      <Modal
        title={t('users.temporaryPermissions.addTitle')}
        open={addOpen}
        onCancel={() => {
          setAddOpen(false);
          form.resetFields();
        }}
        onOk={() => {
          void form.validateFields().then((values) => {
            void upsertMutation.mutateAsync({
              permission: values.permission,
              isGranted: values.isGranted,
              reason: values.reason?.trim() || null,
              validFrom: values.validFrom ? values.validFrom.toISOString() : null,
              expiresAt: values.expiresAt ? values.expiresAt.toISOString() : null,
            });
          });
        }}
        confirmLoading={upsertMutation.isPending}
        destroyOnHidden
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{ isGranted: true }}
        >
          <Form.Item
            name="permission"
            label={t('users.temporaryPermissions.columnPermission')}
            rules={[{ required: true, message: t('users.temporaryPermissions.permissionRequired') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              options={permissionOptions}
              placeholder={t('users.temporaryPermissions.permissionPlaceholder')}
            />
          </Form.Item>
          <Form.Item name="isGranted" label={t('users.temporaryPermissions.columnGrant')}>
            <Select
              options={[
                { value: true, label: t('users.permissionsModal.statusGranted') },
                { value: false, label: t('users.permissionsModal.statusDenied') },
              ]}
            />
          </Form.Item>
          <Form.Item name="validFrom" label={t('users.temporaryPermissions.columnValidFrom')}>
            <DatePicker showTime className="w-full" format={DAYJS_DATE_FORMAT} />
          </Form.Item>
          <Form.Item
            name="expiresAt"
            label={t('users.temporaryPermissions.columnExpiresAt')}
            rules={[{ required: true, message: t('users.temporaryPermissions.expiresRequired') }]}
          >
            <DatePicker showTime className="w-full" format={DAYJS_DATE_FORMAT} />
          </Form.Item>
          <Form.Item name="reason" label={t('users.temporaryPermissions.columnReason')}>
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
