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
  InputNumber,
  Select,
  Switch,
  DatePicker,
} from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import dayjs from 'dayjs';
import {
  useAdminBenefitAssignmentsList,
  useCreateAdminBenefitAssignment,
  useUpdateAdminBenefitAssignment,
  useDeleteAdminBenefitAssignment,
  adminBenefitAssignmentsQueryKeys,
  type BenefitAssignment,
  type CreateBenefitAssignmentRequest,
} from '@/api/admin/benefit-assignments';
import { useAdminBenefitDefinitionsList } from '@/api/admin/benefit-definitions';
import { useGetApiCustomer } from '@/api/generated/customer/customer';
import { useQueryClient } from '@tanstack/react-query';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';

export default function BenefitAssignmentsPage() {
  const queryClient = useQueryClient();
  const listQuery = useAdminBenefitAssignmentsList();
  const definitionsQuery = useAdminBenefitDefinitionsList();
  const customersQuery = useGetApiCustomer({ pageNumber: 1, pageSize: 500 });
  const createMutation = useCreateAdminBenefitAssignment();
  const updateMutation = useUpdateAdminBenefitAssignment();
  const deleteMutation = useDeleteAdminBenefitAssignment();

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<BenefitAssignment | null>(null);
  const [form] = Form.useForm();

  const assignments = listQuery.data ?? [];
  const definitions = definitionsQuery.data ?? [];
  const customerResponse = customersQuery.data as { items?: { id: string; name: string; customerNumber?: string }[] } | undefined;
  const customers = customerResponse?.items ?? [];

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.lists() });

  const handleCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      benefitDefinitionId: undefined,
      customerId: undefined,
      validFrom: dayjs().startOf('day'),
      validTo: null,
      priority: 0,
      isActive: true,
    });
    setModalOpen(true);
  };

  const handleEdit = (record: BenefitAssignment) => {
    setEditing(record);
    form.setFieldsValue({
      benefitDefinitionId: record.benefitDefinitionId,
      customerId: record.customerId,
      validFrom: record.validFrom ? dayjs(record.validFrom) : dayjs(),
      validTo: record.validTo ? dayjs(record.validTo) : null,
      priority: record.priority,
      isActive: record.isActive,
    });
    setModalOpen(true);
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      const payload: CreateBenefitAssignmentRequest = {
        benefitDefinitionId: values.benefitDefinitionId,
        customerId: values.customerId,
        validFrom: values.validFrom?.toISOString?.() ?? new Date().toISOString(),
        validTo: values.validTo ? values.validTo.toISOString() : null,
        priority: values.priority ?? 0,
        isActive: values.isActive ?? true,
      };
      if (editing) {
        await updateMutation.mutateAsync({ id: editing.id, data: payload });
        message.success('Zuweisung aktualisiert');
      } else {
        await createMutation.mutateAsync({ data: payload });
        message.success('Zuweisung angelegt');
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
      message.success('Zuweisung deaktiviert');
      invalidateList();
    } catch {
      message.error('Löschen fehlgeschlagen');
    }
  };

  const columns: ColumnType<BenefitAssignment>[] = [
    {
      title: 'Vorteil',
      key: 'definition',
      render: (_: unknown, r: BenefitAssignment) => r.benefitDefinition?.name ?? r.benefitDefinitionId,
    },
    {
      title: 'Kunde',
      key: 'customer',
      render: (_: unknown, r: BenefitAssignment) => r.customer?.name ?? r.customerId,
    },
    {
      title: 'Gültig von',
      dataIndex: 'validFrom',
      key: 'validFrom',
      render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY') : '–'),
    },
    {
      title: 'Gültig bis',
      dataIndex: 'validTo',
      key: 'validTo',
      render: (v: string | null) => (v ? dayjs(v).format('DD.MM.YYYY') : '–'),
    },
    { title: 'Priorität', dataIndex: 'priority', key: 'priority', align: 'right' },
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
      render: (_: unknown, record: BenefitAssignment) => (
        <Space>
          <Tooltip title="Bearbeiten">
            <Button type="text" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>
          <Popconfirm
            title="Zuweisung deaktivieren?"
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
        title="Vorteile (Zuweisungen)"
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: 'Vorteile (Zuweisungen)' }]}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreate}>
            Neue Zuweisung
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
              {OPERATOR_SHARED_COPY.retryAfterError}
            </Button>
          }
        />
      ) : null}

      <Table
        columns={columns}
        dataSource={assignments}
        rowKey="id"
        loading={listQuery.isLoading}
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description="Keine Zuweisungen" /> }}
      />

      <Modal
        title={editing ? 'Zuweisung bearbeiten' : 'Neue Vorteilszuweisung'}
        open={modalOpen}
        onOk={handleSubmit}
        onCancel={() => { setModalOpen(false); setEditing(null); }}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="benefitDefinitionId" label="Vorteilsdefinition" rules={[{ required: true, message: 'Definition wählen' }]}>
            <Select
              placeholder="Definition wählen"
              loading={definitionsQuery.isLoading}
              options={definitions.filter((d) => d.isActive).map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))}
            />
          </Form.Item>
          <Form.Item name="customerId" label="Kunde" rules={[{ required: true, message: 'Kunde wählen' }]}>
            <Select
              placeholder="Kunde wählen"
              loading={customersQuery.isLoading}
              options={customers.map((c) => ({ value: c.id, label: c.customerNumber ? `${c.customerNumber} – ${c.name}` : c.name }))}
              showSearch
              optionFilterProp="label"
            />
          </Form.Item>
          <Form.Item name="validFrom" label="Gültig von" rules={[{ required: true, message: 'Datum angeben' }]}>
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="validTo" label="Gültig bis">
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="priority" label="Priorität (höher = Vorrang)">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label="Aktiv" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
