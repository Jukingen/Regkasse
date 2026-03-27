'use client';

import React, { useState } from 'react';
import {
  Button,
  Table,
  Space,
  message,
  Modal,
  Form,
  Input,
  InputNumber,
  Switch,
  Select,
  Tag,
} from 'antd';
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
import { useI18n } from '@/i18n/I18nProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

const LEGACY_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: '0 — Bar (Cash)' },
  { value: 1, label: '1 — Karte (Card)' },
  { value: 2, label: '2 — Überweisung (BankTransfer)' },
  { value: 3, label: '3 — Scheck (Check)' },
  { value: 4, label: '4 — Gutschein (Voucher)' },
  { value: 5, label: '5 — Mobil (Mobile)' },
];

export default function PaymentMethodsSettingsPage() {
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
    Modal.confirm({
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
      render: (_, r) => (r.requiresTerminal ? r.terminalType || '—' : '—'),
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
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
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
        />
      </Space>

      <Modal
        title={editing ? t('settings.paymentMethods.editTitle') : t('settings.paymentMethods.createTitle')}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={handleSubmit}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnClose
        width={640}
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
            <Select options={LEGACY_OPTIONS} />
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
            <Input placeholder="cash-outline" />
          </Form.Item>
          <Form.Item name="metadataJson" label={t('settings.paymentMethods.form.metadata')}>
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
}
