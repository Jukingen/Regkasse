'use client';

import React, { useState, useMemo } from 'react';
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
import { PlusOutlined, EditOutlined, DeleteOutlined, FilterOutlined } from '@ant-design/icons';
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
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { useSearchParams } from 'next/navigation';

export default function BenefitAssignmentsPage() {
  const { t } = useI18n();
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

  const assignments = useMemo(() => listQuery.data ?? [], [listQuery.data]);
  const definitions = definitionsQuery.data ?? [];
  const customerResponse = customersQuery.data as { items?: { id: string; name: string; customerNumber?: string }[] } | undefined;
  const customers = useMemo(() => customerResponse?.items ?? [], [customerResponse?.items]);

  // Customer-scoped filter from query param (e.g. navigated from /customers)
  const searchParams = useSearchParams();
  const customerIdParam = searchParams.get('customerId');
  const [filterActive, setFilterActive] = useState(true);
  const filterCustomerId = filterActive ? customerIdParam : null;
  const filterCustomer = useMemo(
    () => (filterCustomerId ? customers.find((c) => c.id === filterCustomerId) : null),
    [filterCustomerId, customers],
  );
  const filteredAssignments = useMemo(
    () => (filterCustomerId ? assignments.filter((a) => a.customerId === filterCustomerId) : assignments),
    [filterCustomerId, assignments],
  );

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.lists() });

  const handleCreate = () => {
    setEditing(null);
    form.setFieldsValue({
      benefitDefinitionId: undefined,
      customerId: filterCustomerId ?? undefined,
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
        message.success(t('benefits.assignments.messages.updated'));
      } else {
        await createMutation.mutateAsync({ data: payload });
        message.success(t('benefits.assignments.messages.created'));
      }
      setModalOpen(false);
      setEditing(null);
      invalidateList();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(editing ? t('benefits.assignments.messages.updateFailed') : t('benefits.assignments.messages.saveFailed'));
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync({ id });
      message.success(t('benefits.assignments.messages.deactivated'));
      invalidateList();
    } catch {
      message.error(t('benefits.assignments.messages.deleteFailed'));
    }
  };

  const columns: ColumnType<BenefitAssignment>[] = [
    {
      title: t('benefits.assignments.columnBenefit'),
      key: 'definition',
      render: (_: unknown, r: BenefitAssignment) => r.benefitDefinition?.name ?? r.benefitDefinitionId,
    },
    {
      title: t('benefits.shared.customer'),
      key: 'customer',
      render: (_: unknown, r: BenefitAssignment) => r.customer?.name ?? r.customerId,
    },
    {
      title: t('benefits.assignments.columnValidFrom'),
      dataIndex: 'validFrom',
      key: 'validFrom',
      render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY') : '–'),
    },
    {
      title: t('benefits.assignments.columnValidTo'),
      dataIndex: 'validTo',
      key: 'validTo',
      render: (v: string | null) => (v ? dayjs(v).format('DD.MM.YYYY') : '–'),
    },
    { title: t('benefits.shared.priority'), dataIndex: 'priority', key: 'priority', align: 'right' },
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
      render: (_: unknown, record: BenefitAssignment) => (
        <Space>
          <Tooltip title={t('benefits.shared.edit')}>
            <Button type="text" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)} />
          </Tooltip>
          <Popconfirm
            title={t('benefits.assignments.popconfirmDeactivate')}
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
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={filterCustomer ? `${t('benefits.assignments.pageTitle')} – ${filterCustomer.name}` : t('benefits.assignments.pageTitle')}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: t('benefits.assignments.breadcrumb') }]}
        actions={
          <Space>
            {customerIdParam && (
              <Button
                icon={<FilterOutlined />}
                onClick={() => {
                  if (filterActive) {
                    setFilterActive(false);
                  } else {
                    setFilterActive(true);
                  }
                }}
              >
                {filterActive ? t('benefits.assignments.showAllCustomers') : t('benefits.assignments.filteringForCustomer')}
              </Button>
            )}
            <Button type="primary" icon={<PlusOutlined />} onClick={handleCreate}>
              {t('benefits.assignments.newAssignment')}
            </Button>
          </Space>
        }
      />

      {listQuery.isError ? (
        <Alert
          type="error"
          message={t('benefits.shared.loadFailedTitle')}
          description={
            listQuery.error ? (
              <ApiErrorAlertDescription
                t={t}
                error={listQuery.error}
                logContext="BenefitAssignments.list"
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
        dataSource={filteredAssignments}
        rowKey="id"
        loading={listQuery.isLoading}
        pagination={{ pageSize: 10, showSizeChanger: true }}
        locale={{ emptyText: <Empty description={t('benefits.assignments.emptyList')} /> }}
      />

      <Modal
        title={editing ? t('benefits.assignments.modalEditTitle') : t('benefits.assignments.modalCreateTitle')}
        open={modalOpen}
        onOk={handleSubmit}
        onCancel={() => { setModalOpen(false); setEditing(null); }}
        confirmLoading={createMutation.isPending || updateMutation.isPending}
        destroyOnClose
        okText={t('common.buttons.save')}
        cancelText={t('common.buttons.cancel')}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="benefitDefinitionId" label={t('benefits.assignments.formDefinition')} rules={[{ required: true, message: t('benefits.assignments.formDefinitionRequired') }]}>
            <Select
              placeholder={t('benefits.assignments.formDefinitionPlaceholder')}
              loading={definitionsQuery.isLoading}
              options={definitions.filter((d) => d.isActive).map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))}
            />
          </Form.Item>
          <Form.Item name="customerId" label={t('benefits.shared.customer')} rules={[{ required: true, message: t('benefits.assignments.formCustomerRequired') }]}>
            <Select
              placeholder={t('benefits.assignments.formCustomerPlaceholder')}
              loading={customersQuery.isLoading}
              options={customers.map((c) => ({ value: c.id, label: c.customerNumber ? `${c.customerNumber} – ${c.name}` : c.name }))}
              showSearch
              optionFilterProp="label"
            />
          </Form.Item>
          <Form.Item name="validFrom" label={t('benefits.assignments.formValidFrom')} rules={[{ required: true, message: t('benefits.assignments.formValidFromRequired') }]}>
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="validTo" label={t('benefits.assignments.formValidTo')}>
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="priority" label={t('benefits.assignments.formPriorityHint')}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label={t('benefits.shared.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
