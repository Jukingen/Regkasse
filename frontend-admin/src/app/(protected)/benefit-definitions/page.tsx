'use client';

import React, { useState } from 'react';
import {
  Button,
  Table,
  Space,
  message,
  Popconfirm,
  Tooltip,
  Empty,
  Alert,
  Modal,
  Form,
  Input,
  InputNumber,
  Select,
  Switch,
} from 'antd';
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

const BENEFIT_KIND_LABELS: Record<AppliedBenefitKind, string> = {
  [AppliedBenefitKind.PercentageDiscount]: 'Prozent-Rabatt',
  [AppliedBenefitKind.FreeAllowance]: 'Kostenlose Tageskontingent',
  [AppliedBenefitKind.BuyXGetY]: 'X kaufen, Y gratis',
};

export default function BenefitDefinitionsPage() {
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
        message.success('Vorteil aktualisiert');
      } else {
        await createMutation.mutateAsync({ data: payload });
        message.success('Vorteil angelegt');
      }
      setModalOpen(false);
      setEditing(null);
      invalidateList();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(editing ? 'Aktualisierung fehlgeschlagen' : 'Anlegen fehlgeschlagen');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync({ id });
      message.success('Vorteil deaktiviert');
      invalidateList();
    } catch {
      message.error('Löschen fehlgeschlagen');
    }
  };

  const columns: ColumnType<BenefitDefinition>[] = [
    { title: 'Code', dataIndex: 'code', key: 'code', width: 120 },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Art',
      dataIndex: 'benefitKind',
      key: 'benefitKind',
      render: (k: AppliedBenefitKind) => BENEFIT_KIND_LABELS[k] ?? k,
    },
    {
      title: '%',
      dataIndex: 'percentageValue',
      key: 'percentageValue',
      align: 'right',
      render: (v: number | null) => (v != null ? `${v}%` : '–'),
    },
    {
      title: 'Aktiv',
      dataIndex: 'isActive',
      key: 'isActive',
      align: 'center',
      render: (v: boolean) => (v ? 'Ja' : 'Nein'),
    },
    {
      title: 'Aktionen',
      key: 'actions',
      align: 'right',
      render: (_: unknown, record: BenefitDefinition) => (
        <Space>
          <Tooltip title="Bearbeiten">
            <Button type="text" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>
          <Popconfirm
            title="Vorteil deaktivieren?"
            onConfirm={() => record.id && handleDelete(record.id)}
            okText="Ja"
            cancelText="Nein"
          >
            <Tooltip title="Deaktivieren">
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
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title="Vorteile (Definitionen)"
        breadcrumbs={[
          { title: 'Dashboard', href: '/dashboard' },
          { title: 'Vorteile (Definitionen)' },
        ]}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreate}>
            Neue Definition
          </Button>
        }
      />

      {listQuery.isError ? (
        <Alert
          type="error"
          message="Laden fehlgeschlagen"
          description={listQuery.error?.message}
          showIcon
          action={
            <Button size="small" onClick={() => listQuery.refetch()}>
              Erneut versuchen
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
        locale={{ emptyText: <Empty description="Keine Definitionen" /> }}
      />

      <Modal
        title={editing ? 'Vorteil bearbeiten' : 'Neue Vorteilsdefinition'}
        open={modalOpen}
        onOk={handleSubmit}
        onCancel={() => { setModalOpen(false); setEditing(null); }}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="code" label="Code" rules={[{ required: true, message: 'Code angeben' }]}>
            <Input placeholder="z.B. STAFF_10" maxLength={50} />
          </Form.Item>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Name angeben' }]}>
            <Input placeholder="z.B. Mitarbeiter 10 %" maxLength={100} />
          </Form.Item>
          <Form.Item name="benefitKind" label="Art" rules={[{ required: true }]}>
            <Select
              options={[
                { value: AppliedBenefitKind.PercentageDiscount, label: BENEFIT_KIND_LABELS[AppliedBenefitKind.PercentageDiscount] },
                { value: AppliedBenefitKind.FreeAllowance, label: BENEFIT_KIND_LABELS[AppliedBenefitKind.FreeAllowance] },
                { value: AppliedBenefitKind.BuyXGetY, label: BENEFIT_KIND_LABELS[AppliedBenefitKind.BuyXGetY] },
              ]}
            />
          </Form.Item>
          <Form.Item name="percentageValue" label="Prozentwert (für Prozent-Rabatt)">
            <InputNumber min={0} max={100} step={0.5} style={{ width: '100%' }} placeholder="10" />
          </Form.Item>
          <Form.Item name="allowanceQuantity" label="Kontingent (für Tageskontingent)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="allowanceScope" label="Scope (z.B. per_day)">
            <Input maxLength={50} placeholder="per_day" />
          </Form.Item>
          <Form.Item name="allowanceCategoryId" label="Kategorie (für Tageskontingent – nur diese Kategorie)">
            <Select
              allowClear
              placeholder="Kategorie wählen"
              loading={categoriesQuery.isLoading}
              options={(categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }))}
            />
          </Form.Item>
          <Form.Item
            name="buyXQuantity"
            label="Buy X (für X kaufen Y gratis)"
            required={benefitKindWatch === AppliedBenefitKind.BuyXGetY}
            dependencies={['benefitKind']}
            rules={[
              {
                validator(_, value) {
                  if (form.getFieldValue('benefitKind') !== AppliedBenefitKind.BuyXGetY) return Promise.resolve();
                  if (value == null || value === '' || Number(value) < 1) {
                    return Promise.reject(new Error('Buy X ist bei „X kaufen, Y gratis“ erforderlich (mind. 1).'));
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
            label="Get Y (für X kaufen Y gratis)"
            required={benefitKindWatch === AppliedBenefitKind.BuyXGetY}
            dependencies={['benefitKind']}
            rules={[
              {
                validator(_, value) {
                  if (form.getFieldValue('benefitKind') !== AppliedBenefitKind.BuyXGetY) return Promise.resolve();
                  if (value == null || value === '' || Number(value) < 1) {
                    return Promise.reject(new Error('Get Y ist bei „X kaufen, Y gratis“ erforderlich (mind. 1).'));
                  }
                  return Promise.resolve();
                },
              },
            ]}
          >
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label="Aktiv" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
