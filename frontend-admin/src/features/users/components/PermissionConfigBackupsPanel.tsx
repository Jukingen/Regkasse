'use client';

import { PlusOutlined, RollbackOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Form,
  Input,
  Modal,
  Space,
  Switch,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type PermissionConfigBackupListItemDto,
  type PermissionConfigRestorePreviewDto,
  createPermissionConfigBackup,
  getPermissionConfigBackupSettings,
  listPermissionConfigBackups,
  previewPermissionConfigRestore,
  restorePermissionConfigBackup,
  setPermissionConfigBackupSettings,
} from '@/features/users/api/permissionConfigBackupsApi';
import { dateColumnRender } from '@/components/DateColumn';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type CreateFormValues = {
  name?: string;
  note?: string;
};

export function PermissionConfigBackupsPanel() {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [form] = Form.useForm<CreateFormValues>();
  const [preview, setPreview] = useState<{
    backup: PermissionConfigBackupListItemDto;
    data: PermissionConfigRestorePreviewDto;
  } | null>(null);

  const listQuery = useQuery({
    queryKey: ['permission-config-backups'],
    queryFn: listPermissionConfigBackups,
  });
  const settingsQuery = useQuery({
    queryKey: ['permission-config-backups', 'settings'],
    queryFn: getPermissionConfigBackupSettings,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['permission-config-backups'] });
  };

  const createMutation = useMutation({
    mutationFn: (values: CreateFormValues) =>
      createPermissionConfigBackup({
        name: values.name?.trim() || null,
        note: values.note?.trim() || null,
      }),
    onSuccess: () => {
      message.success(t('access.permissionBackups.createSuccess'));
      setCreateOpen(false);
      form.resetFields();
      invalidate();
    },
    onError: () => message.error(t('access.permissionBackups.createError')),
  });

  const settingsMutation = useMutation({
    mutationFn: (autoBackupBeforeChanges: boolean) =>
      setPermissionConfigBackupSettings({ autoBackupBeforeChanges }),
    onSuccess: () => {
      message.success(t('access.permissionBackups.settingsSaved'));
      invalidate();
      void queryClient.invalidateQueries({
        queryKey: ['permission-config-backups', 'settings'],
      });
    },
    onError: () => message.error(t('access.permissionBackups.settingsError')),
  });

  const restoreMutation = useMutation({
    mutationFn: (id: string) => restorePermissionConfigBackup(id),
    onSuccess: () => {
      message.success(t('access.permissionBackups.restoreSuccess'));
      setPreview(null);
      invalidate();
    },
    onError: () => message.error(t('access.permissionBackups.restoreError')),
  });

  const openRestorePreview = async (backup: PermissionConfigBackupListItemDto) => {
    try {
      const data = await previewPermissionConfigRestore(backup.id);
      setPreview({ backup, data });
    } catch {
      message.error(t('access.permissionBackups.previewError'));
    }
  };

  const columns: ColumnsType<PermissionConfigBackupListItemDto> = [
    {
      title: t('access.permissionBackups.columnName'),
      dataIndex: 'name',
    },
    {
      title: t('access.permissionBackups.columnTrigger'),
      dataIndex: 'trigger',
      width: 140,
    },
    {
      title: t('access.permissionBackups.columnCreatedAt'),
      dataIndex: 'createdAt',
      width: 180,
      render: dateColumnRender('datetime'),
    },
    {
      title: t('access.permissionBackups.columnNote'),
      dataIndex: 'note',
      ellipsis: true,
      render: (v: string | null | undefined) => v?.trim() || '—',
    },
    {
      key: 'actions',
      width: 140,
      render: (_: unknown, row) => (
        <Button
          type="link"
          size="small"
          icon={<RollbackOutlined />}
          onClick={() => void openRestorePreview(row)}
        >
          {t('access.permissionBackups.restore')}
        </Button>
      ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <Space>
          <Switch
            checked={settingsQuery.data?.autoBackupBeforeChanges ?? false}
            loading={settingsMutation.isPending || settingsQuery.isLoading}
            onChange={(checked) => settingsMutation.mutate(checked)}
          />
          <Typography.Text>{t('access.permissionBackups.autoBackupLabel')}</Typography.Text>
        </Space>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
          {t('access.permissionBackups.create')}
        </Button>
      </div>

      {listQuery.isError ? (
        <Alert type="error" showIcon title={t('access.permissionBackups.loadError')} />
      ) : null}

      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 20 }}
      />

      <Modal
        title={t('access.permissionBackups.createTitle')}
        open={createOpen}
        onCancel={() => {
          setCreateOpen(false);
          form.resetFields();
        }}
        onOk={() => {
          void form.validateFields().then((values) => createMutation.mutate(values));
        }}
        confirmLoading={createMutation.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t('access.permissionBackups.columnName')}>
            <Input maxLength={120} />
          </Form.Item>
          <Form.Item name="note" label={t('access.permissionBackups.columnNote')}>
            <Input.TextArea rows={3} maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('access.permissionBackups.restorePreviewTitle')}
        open={Boolean(preview)}
        onCancel={() => setPreview(null)}
        onOk={() => {
          if (!preview) return;
          modal.confirm({
            title: t('access.permissionBackups.restoreConfirmTitle'),
            content: t('access.permissionBackups.restoreConfirmBody'),
            onOk: () => restoreMutation.mutateAsync(preview.backup.id),
          });
        }}
        okText={t('access.permissionBackups.restore')}
        confirmLoading={restoreMutation.isPending}
        destroyOnHidden
      >
        {preview ? (
          <Space orientation="vertical" style={{ width: '100%' }}>
            <Typography.Text>
              {t('access.permissionBackups.previewRoles', {
                count: preview.data.customRolesChanged,
              })}
            </Typography.Text>
            <Typography.Text>
              {t('access.permissionBackups.previewPackages', {
                count: preview.data.packagesChanged,
              })}
            </Typography.Text>
            <Typography.Text>
              {t('access.permissionBackups.previewOverrides', {
                count: preview.data.overridesChanged,
              })}
            </Typography.Text>
            {preview.data.warnings.length > 0 ? (
              <Alert
                type="warning"
                showIcon
                title={preview.data.warnings.join(' · ')}
              />
            ) : null}
            {preview.data.sampleRoleDeltas.length > 0 ? (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {preview.data.sampleRoleDeltas.slice(0, 8).join('\n')}
              </Typography.Paragraph>
            ) : null}
          </Space>
        ) : null}
      </Modal>
    </div>
  );
}
