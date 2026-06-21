'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Admin: fiyat kuralları (MVP CRUD). Backend: /api/admin/pricing-rules
 */
import React, { useState } from 'react';
import { Modal, Button, Table, Space, Form, Input, InputNumber, Switch, Select, Tag, Typography, Popconfirm } from 'antd';
import { PlusOutlined, EditOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import {
  useAdminPricingRulesList,
  useCreateAdminPricingRule,
  useUpdateAdminPricingRule,
  useDeleteAdminPricingRule,
  type PricingRuleAdmin,
  type CreatePricingRuleRequest,
} from '@/api/admin/pricing-rules';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n/I18nProvider';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

const TARGET_OPTIONS = [
  { value: 0, labelKey: 'settings.pricingRules.form.targetScopeProduct' as const },
  { value: 1, labelKey: 'settings.pricingRules.form.targetScopeCategory' as const },
];

const ACTION_OPTIONS = [
  { value: 0, labelKey: 'settings.pricingRules.form.actionFixed' as const },
  { value: 1, labelKey: 'settings.pricingRules.form.actionPercent' as const },
];

export default function PricingRulesPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const { user } = useAuth();
  const canManage = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);

  const listQuery = useAdminPricingRulesList();
  const createMutation = useCreateAdminPricingRule();
  const updateMutation = useUpdateAdminPricingRule();
  const deleteMutation = useDeleteAdminPricingRule();

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<PricingRuleAdmin | null>(null);
  const [form] = Form.useForm<CreatePricingRuleRequest>();

  const rows = listQuery.data ?? [];

  const headerBreadcrumbs = [adminOverviewCrumb(t), { title: t('settings.pricingRules.title') }];

  const openCreate = () => {
    setEditing(null);
    const today = new Date().toISOString().slice(0, 10);
    form.setFieldsValue({
      name: '',
      priority: 0,
      isActive: true,
      validFromDate: today,
      validToDate: today,
      daysOfWeekMask: 127,
      timeWindowEnabled: false,
      timeStartMinutes: 0,
      timeEndMinutes: 1439,
      targetScope: 0,
      targetId: '',
      actionType: 0,
      actionValue: 0,
      cashRegisterId: undefined,
    });
    setModalOpen(true);
  };

  const openEdit = (row: PricingRuleAdmin) => {
    setEditing(row);
    form.setFieldsValue({
      name: row.name,
      priority: row.priority,
      isActive: row.isActive,
      validFromDate: row.validFromDate.slice(0, 10),
      validToDate: row.validToDate.slice(0, 10),
      daysOfWeekMask: row.daysOfWeekMask,
      timeWindowEnabled: row.timeWindowEnabled,
      timeStartMinutes: row.timeStartMinutes,
      timeEndMinutes: row.timeEndMinutes,
      targetScope: row.targetScope,
      targetId: row.targetId,
      actionType: row.actionType,
      actionValue: row.actionValue,
      cashRegisterId: row.cashRegisterId ?? undefined,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const reg = values.cashRegisterId?.trim();
      const payload: CreatePricingRuleRequest = {
        name: values.name?.trim() ?? '',
        priority: values.priority ?? 0,
        isActive: values.isActive ?? true,
        validFromDate: values.validFromDate,
        validToDate: values.validToDate,
        daysOfWeekMask: values.daysOfWeekMask ?? 127,
        timeWindowEnabled: values.timeWindowEnabled ?? false,
        timeStartMinutes: values.timeStartMinutes ?? 0,
        timeEndMinutes: values.timeEndMinutes ?? 1439,
        targetScope: values.targetScope ?? 0,
        targetId: values.targetId?.trim() ?? '',
        actionType: values.actionType ?? 0,
        actionValue: values.actionValue ?? 0,
        cashRegisterId: reg ? reg : null,
      };
      if (editing) {
        await updateMutation.mutateAsync({ id: editing.id, data: payload });
        message.success(t('settings.pricingRules.saved'));
      } else {
        await createMutation.mutateAsync(payload);
        message.success(t('settings.pricingRules.created'));
      }
      setModalOpen(false);
    } catch (e: unknown) {
      message.error(t('settings.pricingRules.saveFailed'));
    }
  };

  const columns: ColumnType<PricingRuleAdmin>[] = [
    { title: t('settings.pricingRules.columns.name'), dataIndex: 'name', key: 'name' },
    { title: t('settings.pricingRules.columns.priority'), dataIndex: 'priority', key: 'priority', width: 100 },
    {
      title: t('settings.pricingRules.columns.active'),
      dataIndex: 'isActive',
      key: 'isActive',
      width: 90,
      render: (v: boolean) => (v ? <Tag color="green">on</Tag> : <Tag>off</Tag>),
    },
    {
      title: t('settings.pricingRules.columns.validity'),
      key: 'validity',
      render: (_, r) => (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {r.validFromDate.slice(0, 10)} → {r.validToDate.slice(0, 10)}
        </Typography.Text>
      ),
    },
    {
      title: t('settings.pricingRules.columns.target'),
      key: 'target',
      render: (_, r) => (
        <span>
          {r.targetScope === 0 ? t('settings.pricingRules.form.targetScopeProduct') : t('settings.pricingRules.form.targetScopeCategory')}{' '}
          <Typography.Text code copyable={{ text: r.targetId }}>
            {r.targetId.slice(0, 8)}…
          </Typography.Text>
        </span>
      ),
    },
    {
      title: t('settings.pricingRules.columns.action'),
      key: 'action',
      render: (_, r) => (
        <span>
          {r.actionType === 0 ? '€' : '%'} {r.actionValue}
        </span>
      ),
    },
    {
      title: t('settings.pricingRules.columns.actions'),
      key: 'actions',
      width: 200,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(row)} disabled={!canManage}>
            Edit
          </Button>
          {canManage && (
            <Popconfirm
              title={t('settings.pricingRules.deactivateConfirmTitle')}
              description={t('settings.pricingRules.deactivateConfirmBody')}
              onConfirm={async () => {
                try {
                  await deleteMutation.mutateAsync(row.id);
                  message.success(t('settings.pricingRules.deactivated'));
                } catch {
                  message.error(t('settings.pricingRules.saveFailed'));
                }
              }}
              okText={t('settings.pricingRules.deactivate')}
            >
              <Button type="link" size="small" danger>
                {t('settings.pricingRules.deactivate')}
              </Button>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: 24 }}>
      <AdminPageHeader title={t('settings.pricingRules.title')} breadcrumbs={headerBreadcrumbs} />
      <Typography.Paragraph type="secondary">{t('settings.pricingRules.subtitle')}</Typography.Paragraph>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} disabled={!canManage}>
          {t('settings.pricingRules.add')}
        </Button>
      </Space>
      <Table<PricingRuleAdmin>
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={{ pageSize: 15 }}
      />

      <Modal
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={handleSubmit}
        okButtonProps={{ loading: createMutation.isPending || updateMutation.isPending }}
        title={editing ? t('settings.pricingRules.editTitle') : t('settings.pricingRules.createTitle')}
        width={640}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t('settings.pricingRules.form.name')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="priority" label={t('settings.pricingRules.form.priority')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label={t('settings.pricingRules.form.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="validFromDate" label={t('settings.pricingRules.form.validFrom')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="validToDate" label={t('settings.pricingRules.form.validTo')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Input type="date" />
          </Form.Item>
          <Form.Item name="daysOfWeekMask" label={t('settings.pricingRules.form.daysMask')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <InputNumber min={1} max={127} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="timeWindowEnabled" label={t('settings.pricingRules.form.timeWindow')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item name="timeStartMinutes" label={t('settings.pricingRules.form.timeStart')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <InputNumber min={0} max={1439} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="timeEndMinutes" label={t('settings.pricingRules.form.timeEnd')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <InputNumber min={0} max={1439} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="targetScope" label={t('settings.pricingRules.form.targetScope')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Select
              options={TARGET_OPTIONS.map((o) => ({
                value: o.value,
                label: t(o.labelKey),
              }))}
            />
          </Form.Item>
          <Form.Item name="targetId" label={t('settings.pricingRules.form.targetId')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="actionType" label={t('settings.pricingRules.form.actionType')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <Select
              options={ACTION_OPTIONS.map((o) => ({
                value: o.value,
                label: t(o.labelKey),
              }))}
            />
          </Form.Item>
          <Form.Item name="actionValue" label={t('settings.pricingRules.form.actionValue')} rules={[{ required: true, message: t('common.validation.fieldRequired') }]}>
            <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="cashRegisterId" label={t('settings.pricingRules.form.cashRegisterId')}>
            <Input placeholder={t('settings.pricingRules.form.placeholderOptional')} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
