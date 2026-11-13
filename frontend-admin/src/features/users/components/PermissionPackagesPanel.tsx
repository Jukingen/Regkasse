'use client';

import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
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
import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type PermissionPackageDto,
  createPermissionPackage,
  deletePermissionPackage,
  listPermissionPackages,
  updatePermissionPackage,
} from '@/features/users/api/permissionPackagesApi';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type PackageFormValues = {
  name: string;
  slug?: string;
  description?: string;
  permissions: string[];
};

export function PermissionPackagesPanel() {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const catalogQuery = usePermissionsCatalog({ enabled: true });
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<PermissionPackageDto | null>(null);
  const [form] = Form.useForm<PackageFormValues>();

  const listQuery = useQuery({
    queryKey: ['permission-packages'],
    queryFn: listPermissionPackages,
  });

  const invalidate = () => void queryClient.invalidateQueries({ queryKey: ['permission-packages'] });

  const saveMutation = useMutation({
    mutationFn: async (values: PackageFormValues) => {
      const body = {
        name: values.name.trim(),
        slug: values.slug?.trim() || undefined,
        description: values.description?.trim() || null,
        permissions: values.permissions ?? [],
      };
      if (editing) return updatePermissionPackage(editing.id, body);
      return createPermissionPackage(body);
    },
    onSuccess: () => {
      message.success(t('access.permissionPackages.saveSuccess'));
      setEditorOpen(false);
      setEditing(null);
      form.resetFields();
      invalidate();
    },
    onError: () => message.error(t('access.permissionPackages.saveError')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deletePermissionPackage(id),
    onSuccess: () => {
      message.success(t('access.permissionPackages.deleteSuccess'));
      invalidate();
    },
    onError: () => message.error(t('access.permissionPackages.deleteError')),
  });

  const permissionOptions = useMemo(
    () =>
      (catalogQuery.data ?? []).map((item) => ({
        value: item.key,
        label: `${resolvePermissionDisplayLabel(item.key, t)} (${item.key})`,
      })),
    [catalogQuery.data, t]
  );

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ permissions: [] });
    setEditorOpen(true);
  };

  const openEdit = (row: PermissionPackageDto) => {
    setEditing(row);
    form.setFieldsValue({
      name: row.name,
      slug: row.slug,
      description: row.description ?? undefined,
      permissions: row.permissions,
    });
    setEditorOpen(true);
  };

  const columns: ColumnsType<PermissionPackageDto> = [
    {
      title: t('access.permissionPackages.columnName'),
      dataIndex: 'name',
      render: (name: string, row) => (
        <Space>
          <span>{name}</span>
          {row.isSystem ? <Tag>{t('access.permissionPackages.systemTag')}</Tag> : null}
        </Space>
      ),
    },
    {
      title: t('access.permissionPackages.columnSlug'),
      dataIndex: 'slug',
      width: 160,
    },
    {
      title: t('access.permissionPackages.columnCount'),
      dataIndex: 'permissionCount',
      width: 100,
    },
    {
      title: t('access.permissionPackages.columnDescription'),
      dataIndex: 'description',
      ellipsis: true,
      render: (v: string | null | undefined) => v?.trim() || '—',
    },
    {
      key: 'actions',
      width: 160,
      render: (_: unknown, row) => (
        <Space>
          <Button
            type="link"
            size="small"
            icon={<EditOutlined />}
            disabled={row.isSystem}
            onClick={() => openEdit(row)}
          >
            {t('common.buttons.edit')}
          </Button>
          <Button
            type="link"
            danger
            size="small"
            icon={<DeleteOutlined />}
            disabled={row.isSystem}
            onClick={() => {
              modal.confirm({
                title: t('access.permissionPackages.deleteConfirmTitle'),
                content: t('access.permissionPackages.deleteConfirmBody'),
                onOk: () => deleteMutation.mutateAsync(row.id),
              });
            }}
          >
            {t('common.buttons.delete')}
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('access.permissionPackages.intro')}
        </Typography.Paragraph>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('access.permissionPackages.create')}
        </Button>
      </div>

      {listQuery.isError ? (
        <Alert type="error" showIcon title={t('access.permissionPackages.loadError')} />
      ) : null}

      <Table
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 20 }}
      />

      <Modal
        title={
          editing
            ? t('access.permissionPackages.editTitle')
            : t('access.permissionPackages.createTitle')
        }
        open={editorOpen}
        onCancel={() => {
          setEditorOpen(false);
          setEditing(null);
          form.resetFields();
        }}
        onOk={() => {
          void form.validateFields().then((values) => saveMutation.mutate(values));
        }}
        confirmLoading={saveMutation.isPending}
        width={720}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('access.permissionPackages.columnName')}
            rules={[{ required: true }]}
          >
            <Input maxLength={120} />
          </Form.Item>
          <Form.Item name="slug" label={t('access.permissionPackages.columnSlug')}>
            <Input maxLength={80} disabled={Boolean(editing)} />
          </Form.Item>
          <Form.Item name="description" label={t('access.permissionPackages.columnDescription')}>
            <Input.TextArea rows={2} maxLength={500} />
          </Form.Item>
          <Form.Item
            name="permissions"
            label={t('access.permissionPackages.permissionsLabel')}
            rules={[{ required: true, message: t('access.permissionPackages.permissionsRequired') }]}
          >
            <Select
              mode="multiple"
              showSearch
              optionFilterProp="label"
              options={permissionOptions}
              placeholder={t('access.permissionPackages.permissionsPlaceholder')}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
