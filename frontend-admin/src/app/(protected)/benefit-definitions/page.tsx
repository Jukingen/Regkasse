'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useState } from 'react';
import { Modal, Button, Table, Space, Popconfirm, Tooltip, Empty, Alert, Form, Input, InputNumber, Select, Switch } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import {
  useAdminBenefitDefinitionsList,
  useCreateAdminBenefitDefinition,
  useUpdateAdminBenefitDefinition,
  useDeleteAdminBenefitDefinition,
  adminBenefitDefinitionsQueryKeys,
  type BenefitDefinition,
  type CreateBenefitDefinitionRequest,
  AppliedBenefitKind,
} from '@/api/admin/benefit-definitions';
import { useAdminCategoriesList } from '@/api/admin/categories';
import { useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

export default function BenefitDefinitionsPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();

  const benefitKindLabel = (k: AppliedBenefitKind) => {
    switch (k) {
      case AppliedBenefitKind.PercentageDiscount:
        return t('benefits.definitions.kindPercentage');
      case AppliedBenefitKind.FreeAllowance:
        return t('benefits.definitions.kindAllowance');
      case AppliedBenefitKind.BuyXGetY:
        return t('benefits.definitions.kindBuyXGetY');
      default:
        return String(k);
    }
  };
  const queryClient = useQueryClient();
  const listQuery = useAdminBenefitDefinitionsList();
  const categoriesQuery = useAdminCategoriesList();
  const createMutation = useCreateAdminBenefitDefinition();
  const updateMutation = useUpdateAdminBenefitDefinition();
  const deleteMutation = useDeleteAdminBenefitDefinition();

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<BenefitDefinition | null>(null);
  const [form] = Form.useForm();
  const benefitKindWatch = Form.useWatch('benefitKind', form);

  const definitions = listQuery.data ?? [];
  const invalidateList = () => queryClient.invalidateQueries({ queryKey: adminBenefitDefinitionsQueryKeys.lists() });

  const handleCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      code: '',
      name: '',
      benefitKind: AppliedBenefitKind.PercentageDiscount,
      percentageValue: null,
      allowanceQuantity: null,
      allowanceScope: 'per_day',
      allowanceCategoryId: undefined,
      buyXQuantity: null,
      getYQuantity: null,
      isActive: true,
    });
    setModalOpen(true);
  };

  const handleEdit = (record: BenefitDefinition) => {
    setEditing(record);
    form.setFieldsValue({
      code: record.code,
      name: record.name,
      benefitKind: record.benefitKind,
      percentageValue: record.percentageValue ?? undefined,
      allowanceQuantity: record.allowanceQuantity ?? undefined,
      allowanceScope: record.allowanceScope ?? 'per_day',
      allowanceCategoryId: record.allowanceCategoryId ?? undefined,
      buyXQuantity: record.buyXQuantity ?? undefined,
      getYQuantity: record.getYQuantity ?? undefined,
      isActive: record.isActive,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const payload: CreateBenefitDefinitionRequest = {
        code: values.code?.trim() ?? '',
        name: values.name?.trim() ?? '',
        benefitKind: values.benefitKind ?? AppliedBenefitKind.PercentageDiscount,
        percentageValue: values.percentageValue ?? null,
        allowanceQuantity: values.allowanceQuantity ?? null,
        allowanceScope: values.allowanceScope?.trim() || null,
        allowanceCategoryId: values.allowanceCategoryId ?? null,
        buyXQuantity: values.buyXQuantity ?? null,
        getYQuantity: values.getYQuantity ?? null,
        isActive: values.isActive ?? true,
      };
      if (editing) {
        await updateMutation.mutateAsync({ id: editing.id, data: payload });
        message.success(t('benefits.definitions.messages.updated'));
      } else {
        await createMutation.mutateAsync({ data: payload });
        message.success(t('benefits.definitions.messages.created'));
      }
      setModalOpen(false);
      setEditing(null);
      invalidateList();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(editing ? t('benefits.definitions.messages.updateFailed') : t('benefits.definitions.messages.saveFailed'));
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync({ id });
      message.success(t('benefits.definitions.messages.deactivated'));
      invalidateList();
    } catch {
      message.error(t('benefits.definitions.messages.deleteFailed'));
    }
  };

  const columns: ColumnType<BenefitDefinition>[] = [
    { title: t('benefits.shared.code'), dataIndex: 'code', key: 'code', width: 120 },
    { title: t('benefits.shared.name'), dataIndex: 'name', key: 'name' },
    {
      title: t('benefits.shared.kind'),
      dataIndex: 'benefitKind',
      key: 'benefitKind',
      render: (k: AppliedBenefitKind) => benefitKindLabel(k),
    },
    {
      title: t('benefits.definitions.columnPercent'),
      dataIndex: 'percentageValue',
      key: 'percentageValue',
      align: 'right',
      render: (v: number | null) => (v != null ? `${v}%` : '–'),
    },
    {
      title: t('benefits.shared.active'),
      dataIndex: 'isActive',
      key: 'isActive',
      align: 'center',
      render: (v: boolean) => (v ? t('benefits.shared.yes') : t('benefits.shared.no')),
    },
    {
      title: t('benefits.shared.actions'),
      key: 'actions',
      align: 'right',
      render: (_: unknown, record: BenefitDefinition) => (
        <Space>
          <Tooltip title={t('benefits.shared.edit')}>
            <Button type="text" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>
          <Popconfirm
            title={t('benefits.definitions.popconfirmDeactivate')}
            onConfirm={() => record.id && handleDelete(record.id)}
            okText={t('common.buttons.yes')}
            cancelText={t('common.buttons.no')}
          >
            <Tooltip title={t('benefits.shared.deactivate')}>
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                loading={deleteMutation.isPending && deleteMutation.variables?.id === record.id}
              />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('benefits.definitions.pageTitle')}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: t('benefits.definitions.breadcrumb') }]}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreate}>
            {t('benefits.definitions.newDefinition')}
          </Button>
        }
      />

      {listQuery.isError ? (
        <Alert
          type="error"
          title={t('benefits.shared.loadFailedTitle')}
          description={
            listQuery.error ? (
              <ApiErrorAlertDescription
                t={t}
                error={listQuery.error}
                logContext="BenefitDefinitions.list"
                fallbackKey="common.messages.unknownError"
              />
            ) : undefined
          }
          showIcon
          action={
            <Button size="small" onClick={() => listQuery.refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : null}

      <Table
        columns={columns}
        dataSource={definitions}
        rowKey="id"
        loading={listQuery.isLoading}
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description={t('benefits.definitions.emptyList')} /> }}
      />

      <Modal
        title={editing ? t('benefits.definitions.modalEditTitle') : t('benefits.definitions.modalCreateTitle')}
        open={modalOpen}
        onOk={handleSubmit}
        onCancel={() => { setModalOpen(false); setEditing(null); }}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnHidden
        okText={t('common.buttons.save')}
        cancelText={t('common.buttons.cancel')}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="code" label={t('benefits.shared.code')} rules={[{ required: true, message: t('benefits.definitions.formCodeRequired') }]}>
            <Input placeholder={t('benefits.definitions.codePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="name" label={t('benefits.shared.name')} rules={[{ required: true, message: t('benefits.definitions.formNameRequired') }]}>
            <Input placeholder={t('benefits.definitions.namePlaceholder')} maxLength={100} />
          </Form.Item>
          <Form.Item name="benefitKind" label={t('benefits.shared.kind')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: AppliedBenefitKind.PercentageDiscount, label: benefitKindLabel(AppliedBenefitKind.PercentageDiscount) },
                { value: AppliedBenefitKind.FreeAllowance, label: benefitKindLabel(AppliedBenefitKind.FreeAllowance) },
                { value: AppliedBenefitKind.BuyXGetY, label: benefitKindLabel(AppliedBenefitKind.BuyXGetY) },
              ]}
            />
          </Form.Item>
          <Form.Item name="percentageValue" label={t('benefits.definitions.formPercentValue')}>
            <InputNumber min={0} max={100} step={0.5} style={{ width: '100%' }} placeholder="10" />
          </Form.Item>
          <Form.Item name="allowanceQuantity" label={t('benefits.definitions.formAllowanceQty')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="allowanceScope" label={t('benefits.definitions.formAllowanceScope')}>
            <Input maxLength={50} placeholder={t('benefits.definitions.formAllowanceScopePlaceholder')} />
          </Form.Item>
          <Form.Item name="allowanceCategoryId" label={t('benefits.definitions.formAllowanceCategory')}>
            <Select
              allowClear
              placeholder={t('benefits.shared.selectCategory')}
              loading={categoriesQuery.isLoading}
              options={(categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }))}
            />
          </Form.Item>
          <Form.Item
            name="buyXQuantity"
            label={t('benefits.definitions.formBuyX')}
            required={benefitKindWatch === AppliedBenefitKind.BuyXGetY}
            dependencies={['benefitKind']}
            rules={[
              {
                validator(_, value) {
                  if (form.getFieldValue('benefitKind') !== AppliedBenefitKind.BuyXGetY) return Promise.resolve();
                  if (value == null || value === '' || Number(value) < 1) {
                    return Promise.reject(new Error(t('benefits.definitions.validationBuyX')));
                  }
                  return Promise.resolve();
                },
              },
            ]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            name="getYQuantity"
            label={t('benefits.definitions.formGetY')}
            required={benefitKindWatch === AppliedBenefitKind.BuyXGetY}
            dependencies={['benefitKind']}
            rules={[
              {
                validator(_, value) {
                  if (form.getFieldValue('benefitKind') !== AppliedBenefitKind.BuyXGetY) return Promise.resolve();
                  if (value == null || value === '' || Number(value) < 1) {
                    return Promise.reject(new Error(t('benefits.definitions.validationGetY')));
                  }
                  return Promise.resolve();
                },
              },
            ]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label={t('benefits.shared.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
