'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useMemo, useState } from 'react';
import { Modal, Button, Table, Space, Form, Input, InputNumber, Switch, Select, Tag, Typography, Empty } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import {
  useAdminPaymentMethodDefinitionsList,
  useCreateAdminPaymentMethodDefinition,
  useUpdateAdminPaymentMethodDefinition,
  useDeleteAdminPaymentMethodDefinition,
  type PaymentMethodDefinitionAdmin,
  type CreatePaymentMethodDefinitionRequest,
} from '@/api/admin/payment-method-definitions';
import { useQueryClient } from '@tanstack/react-query';
import { adminPaymentMethodDefinitionsQueryKeys } from '@/api/admin/payment-method-definitions';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

export default function PaymentMethodsSettingsPage() {
  const { message, modal } = useAntdApp();

  const { t } = useI18n();
  const { user } = useAuth();
  const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const queryClient = useQueryClient();
  const listQuery = useAdminPaymentMethodDefinitionsList();
  const createMutation = useCreateAdminPaymentMethodDefinition();
  const updateMutation = useUpdateAdminPaymentMethodDefinition();
  const deleteMutation = useDeleteAdminPaymentMethodDefinition();

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<PaymentMethodDefinitionAdmin | null>(null);
  const [form] = Form.useForm<CreatePaymentMethodDefinitionRequest>();

  const rows = listQuery.data ?? [];

  const legacyPaymentMethodOptions = useMemo(
    () => [
      { value: 0, label: t('settings.paymentMethods.form.legacyOption0') },
      { value: 1, label: t('settings.paymentMethods.form.legacyOption1') },
      { value: 2, label: t('settings.paymentMethods.form.legacyOption2') },
      { value: 3, label: t('settings.paymentMethods.form.legacyOption3') },
      { value: 4, label: t('settings.paymentMethods.form.legacyOption4') },
      { value: 5, label: t('settings.paymentMethods.form.legacyOption5') },
    ],
    [t],
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: adminPaymentMethodDefinitionsQueryKeys.lists() });

  const openCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      code: '',
      name: '',
      legacyPaymentMethodValue: 1,
      fiscalCategory: '',
      isActive: true,
      isDefault: false,
      displayOrder: 100,
      requiresTerminal: false,
      terminalType: '',
      allowRefund: true,
      icon: '',
      metadataJson: '',
    });
    setModalOpen(true);
  };

  const openEdit = (row: PaymentMethodDefinitionAdmin) => {
    setEditing(row);
    form.setFieldsValue({
      code: row.code,
      name: row.name,
      legacyPaymentMethodValue: row.legacyPaymentMethodValue,
      fiscalCategory: row.fiscalCategory ?? '',
      isActive: row.isActive,
      isDefault: row.isDefault,
      displayOrder: row.displayOrder,
      requiresTerminal: row.requiresTerminal,
      terminalType: row.terminalType ?? '',
      allowRefund: row.allowRefund,
      icon: row.icon ?? '',
      metadataJson: row.metadataJson ?? '',
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const payload: CreatePaymentMethodDefinitionRequest = {
        code: values.code?.trim() ?? '',
        name: values.name?.trim() ?? '',
        legacyPaymentMethodValue: values.legacyPaymentMethodValue,
        fiscalCategory: values.fiscalCategory?.trim() || null,
        isActive: values.isActive ?? true,
        isDefault: values.isDefault ?? false,
        displayOrder: values.displayOrder ?? 0,
        requiresTerminal: values.requiresTerminal ?? false,
        terminalType: values.terminalType?.trim() || null,
        allowRefund: values.allowRefund ?? true,
        icon: values.icon?.trim() || null,
        metadataJson: values.metadataJson?.trim() || null,
      };
      if (editing) {
        await updateMutation.mutateAsync({ id: editing.id, data: payload });
        message.success(t('settings.paymentMethods.saved'));
      } else {
        await createMutation.mutateAsync(payload);
        message.success(t('settings.paymentMethods.created'));
      }
      setModalOpen(false);
      setEditing(null);
      invalidate();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(t('settings.paymentMethods.saveFailed'));
    }
  };

  const handleDeactivate = (row: PaymentMethodDefinitionAdmin) => {
    modal.confirm({
      title: t('settings.paymentMethods.deactivateConfirmTitle'),
      content: t('settings.paymentMethods.deactivateConfirmBody', { code: row.code }),
      okText: t('common.buttons.yes'),
      cancelText: t('common.buttons.cancel'),
      onOk: async () => {
        await deleteMutation.mutateAsync(row.id);
        message.success(t('settings.paymentMethods.deactivated'));
        invalidate();
      },
    });
  };

  const columns: ColumnType<PaymentMethodDefinitionAdmin>[] = [
    { title: t('settings.paymentMethods.columns.code'), dataIndex: 'code', key: 'code', width: 140 },
    { title: t('settings.paymentMethods.columns.name'), dataIndex: 'name', key: 'name' },
    {
      title: t('settings.paymentMethods.columns.active'),
      dataIndex: 'isActive',
      key: 'isActive',
      width: 100,
      render: (v: boolean) => (v ? <Tag color="green">{t('common.buttons.yes')}</Tag> : <Tag>{t('common.buttons.no')}</Tag>),
    },
    {
      title: t('settings.paymentMethods.columns.default'),
      dataIndex: 'isDefault',
      key: 'isDefault',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="blue">{t('common.buttons.yes')}</Tag> : <Tag>{t('common.buttons.no')}</Tag>),
    },
    { title: t('settings.paymentMethods.columns.order'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    {
      title: t('settings.paymentMethods.columns.legacy'),
      dataIndex: 'legacyPaymentMethodValue',
      key: 'legacyPaymentMethodValue',
      width: 110,
    },
    {
      title: t('settings.paymentMethods.columns.terminal'),
      key: 'term',
      width: 120,
      render: (_, r) =>
        r.requiresTerminal ? r.terminalType?.trim() || FORMAT_EMPTY_DISPLAY : FORMAT_EMPTY_DISPLAY,
    },
    {
      title: t('settings.paymentMethods.columns.actions'),
      key: 'actions',
      width: 200,
      render: (_, row) => (
        <Space>
          {canManage && (
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>
              {t('common.buttons.edit')}
            </Button>
          )}
          {canManage && row.isActive && (
            <Button type="link" size="small" danger onClick={() => handleDeactivate(row)}>
              {t('settings.paymentMethods.deactivate')}
            </Button>
          )}
        </Space>
      ),
    },
  ];

  const headerBreadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.paymentMethods.title') },
  ];

  return (
    <>
      <AdminPageHeader title={t('settings.paymentMethods.title')} breadcrumbs={headerBreadcrumbs} />
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('settings.paymentMethods.intro')}
        </Typography.Paragraph>
        <Space>
          {canManage && (
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('settings.paymentMethods.add')}
            </Button>
          )}
        </Space>
        <Table<PaymentMethodDefinitionAdmin>
          rowKey="id"
          loading={listQuery.isLoading}
          dataSource={rows}
          columns={columns}
          pagination={{ pageSize: 20 }}
          locale={{
            emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('settings.paymentMethods.tableEmpty')} />,
          }}
        />
      </Space>

      <Modal
        title={editing ? t('settings.paymentMethods.editTitle') : t('settings.paymentMethods.createTitle')}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={handleSubmit}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnHidden
        width={640}
        okText={t('common.buttons.save')}
        cancelText={t('common.buttons.cancel')}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="code"
            label={t('settings.paymentMethods.form.code')}
            rules={[{ required: true }]}
            extra={t('settings.paymentMethods.form.codeHint')}
          >
            <Input disabled={!!editing} autoComplete="off" />
          </Form.Item>
          <Form.Item name="name" label={t('settings.paymentMethods.form.name')} rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="legacyPaymentMethodValue" label={t('settings.paymentMethods.form.legacy')} rules={[{ required: true }]}>
            <Select options={legacyPaymentMethodOptions} />
          </Form.Item>
          <Form.Item name="fiscalCategory" label={t('settings.paymentMethods.form.fiscalCategory')}>
            <Input />
          </Form.Item>
          <Form.Item name="displayOrder" label={t('settings.paymentMethods.form.order')}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label={t('settings.paymentMethods.form.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="isDefault" label={t('settings.paymentMethods.form.default')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="requiresTerminal" label={t('settings.paymentMethods.form.requiresTerminal')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="terminalType" label={t('settings.paymentMethods.form.terminalType')}>
            <Input />
          </Form.Item>
          <Form.Item name="allowRefund" label={t('settings.paymentMethods.form.allowRefund')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="icon" label={t('settings.paymentMethods.form.icon')}>
            <Input placeholder={t('settings.paymentMethods.form.iconPlaceholder')} />
          </Form.Item>
          <Form.Item name="metadataJson" label={t('settings.paymentMethods.form.metadata')}>
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
