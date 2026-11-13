'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Form, Input, InputNumber, Space, Switch, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';

import {
  adminTicketPriceCategoryQueryKeys,
  createAdminTicketPriceCategory,
  updateAdminTicketPriceCategory,
  useAdminTicketPriceCategories,
  type AdminTicketPriceCategory,
  type UpsertTicketPriceCategoryBody,
} from '@/api/admin/ticket-price-categories';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

type CreateValues = {
  code: string;
  name: string;
};

function CategoryNameCell({
  row,
  disabled,
  onSave,
}: {
  row: AdminTicketPriceCategory;
  disabled: boolean;
  onSave: (body: UpsertTicketPriceCategoryBody) => void;
}) {
  const { t } = useI18n();
  const [name, setName] = useState(row.name);
  return (
    <Input
      aria-label={`${t('products.ticketEvents.name')} ${row.code}`}
      value={name}
      disabled={disabled}
      onChange={(event) => setName(event.target.value)}
      onBlur={() => {
        const next = name.trim();
        if (!disabled && next && next !== row.name) {
          onSave({ code: row.code, name: next, sortOrder: row.sortOrder, isActive: row.isActive });
        }
      }}
    />
  );
}

function CategorySortCell({
  row,
  disabled,
  onSave,
}: {
  row: AdminTicketPriceCategory;
  disabled: boolean;
  onSave: (body: UpsertTicketPriceCategoryBody) => void;
}) {
  const { t } = useI18n();
  const [sortOrder, setSortOrder] = useState(row.sortOrder);
  return (
    <InputNumber
      aria-label={`${t('products.ticketEvents.sortOrder')} ${row.code}`}
      value={sortOrder}
      disabled={disabled}
      onChange={(value) => setSortOrder(typeof value === 'number' ? value : 0)}
      onBlur={() => {
        if (!disabled && sortOrder !== row.sortOrder) {
          onSave({ code: row.code, name: row.name, sortOrder, isActive: row.isActive });
        }
      }}
    />
  );
}

export function TicketPriceCategoriesPanel() {
  const { t } = useI18n();
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canManage = hasPermission(PERMISSIONS.PRODUCT_MANAGE);
  const queryClient = useQueryClient();
  const [form] = Form.useForm<CreateValues>();
  const list = useAdminTicketPriceCategories();

  const save = useMutation({
    mutationFn: (input: { id?: string; body: UpsertTicketPriceCategoryBody }) =>
      input.id
        ? updateAdminTicketPriceCategory(input.id, input.body)
        : createAdminTicketPriceCategory(input.body),
    onSuccess: async () => {
      notify.success(t('products.ticketEvents.categorySaved'));
      form.resetFields();
      await queryClient.invalidateQueries({ queryKey: adminTicketPriceCategoryQueryKeys.all });
    },
    onError: (error) => {
      notify.apiError(error, {
        logContext: 'TicketPriceCategoriesPanel.save',
        fallbackKey: 'products.ticketEvents.categoryError',
      });
    },
  });

  const persist = (id: string, body: UpsertTicketPriceCategoryBody) => save.mutate({ id, body });

  const columns: ColumnsType<AdminTicketPriceCategory> = [
    { title: t('products.ticketEvents.categoryCode'), dataIndex: 'code' },
    {
      title: t('products.ticketEvents.name'),
      dataIndex: 'name',
      render: (_value, row) => (
        <CategoryNameCell row={row} disabled={!canManage} onSave={(body) => persist(row.id, body)} />
      ),
    },
    {
      title: t('products.ticketEvents.sortOrder'),
      dataIndex: 'sortOrder',
      render: (_value, row) => (
        <CategorySortCell row={row} disabled={!canManage} onSave={(body) => persist(row.id, body)} />
      ),
    },
    {
      title: t('products.ticketEvents.active'),
      dataIndex: 'isActive',
      render: (_value, row) => (
        <Switch
          aria-label={`${t('products.ticketEvents.active')} ${row.code}`}
          checked={row.isActive}
          disabled={!canManage}
          onChange={(checked) =>
            persist(row.id, {
              code: row.code,
              name: row.name,
              sortOrder: row.sortOrder,
              isActive: checked,
            })
          }
        />
      ),
    },
  ];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      {canManage ? (
        <Form<CreateValues>
          form={form}
          layout="inline"
          onFinish={(values) => {
            save.mutate({
              body: {
                code: values.code.trim(),
                name: values.name.trim(),
                sortOrder: 0,
                isActive: true,
              },
            });
          }}
        >
          <Form.Item
            name="code"
            label={t('products.ticketEvents.categoryCode')}
            rules={[{ required: true }]}
          >
            <Input aria-label={t('products.ticketEvents.categoryCode')} />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('products.ticketEvents.name')}
            rules={[{ required: true }]}
          >
            <Input aria-label={t('products.ticketEvents.name')} />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={save.isPending}>
              {t('products.ticketEvents.newCategory')}
            </Button>
          </Form.Item>
        </Form>
      ) : null}
      {list.isError ? (
        <Typography.Text type="danger">{t('products.ticketEvents.loadError')}</Typography.Text>
      ) : null}
      <Table
        rowKey={(row) => row.id}
        loading={list.isLoading}
        dataSource={list.data ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: t('products.ticketEvents.empty') }}
      />
    </Space>
  );
}
