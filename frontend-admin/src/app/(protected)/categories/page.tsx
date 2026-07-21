'use client';

import { PlusOutlined } from '@ant-design/icons';
import type { UseQueryOptions } from '@tanstack/react-query';
import { keepPreviousData } from '@tanstack/react-query';
import { Alert, Button, Empty, Flex, Input, Space, Spin, Table, Typography } from 'antd';
import React, { useEffect, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { CategoryFormSubmitValues } from '@/features/categories/components/CategoryForm';
import CategoryForm from '@/features/categories/components/CategoryForm';
import CategoryTable from '@/features/categories/components/CategoryTable';
import { ResetCategoriesButton } from '@/features/categories/components/ResetCategoriesButton';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { AdminCategory } from '@/features/categories/types';
import { buildCategoryUpdatePayload, categoryTaxRate } from '@/features/categories/types';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

const SEARCH_DEBOUNCE_MS = 400;

function CategoryProducts({ categoryId }: { categoryId: string }) {
  const { t } = useI18n();
  const { useProductsByCategory } = useCategories();
  const { data: products, isLoading, isError, error, refetch } = useProductsByCategory(categoryId);

  if (isLoading) {
    return (
      <div style={{ padding: 16, textAlign: 'center' }}>
        <Spin size="small" />
      </div>
    );
  }
  if (isError) {
    return (
      <div style={{ padding: 16 }}>
        <Alert
          type="error"
          title={t('common.categories.productsLoadError')}
          description={
            error ? (
              <ApiErrorAlertDescription
                t={t}
                error={error}
                logContext="CategoryProducts"
                fallbackKey="common.messages.unknownError"
              />
            ) : undefined
          }
          action={
            <Button size="small" onClick={() => refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      </div>
    );
  }
  if (!products?.length) {
    return (
      <div style={{ padding: 16 }}>
        <Empty
          description={t('common.categories.emptyCategoryProducts')}
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  return (
    <div style={{ padding: '8px 16px 16px' }}>
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
        {t('common.categories.directlyAssignedProducts')}{' '}
        <Typography.Text strong>{products.length}</Typography.Text>{' '}
        {products.length === 1
          ? t('common.categories.productSingular')
          : t('common.categories.productPlural')}
      </Typography.Text>
      <Table
        size="small"
        dataSource={products}
        rowKey="id"
        pagination={{ pageSize: 10, showSizeChanger: true, hideOnSinglePage: true }}
        columns={[
          {
            title: t('common.categories.table.product'),
            dataIndex: 'name',
            key: 'name',
            ellipsis: true,
          },
          {
            title: t('common.categories.table.barcode'),
            dataIndex: 'barcode',
            key: 'barcode',
            width: 120,
            render: (v: string | null | undefined) =>
              v?.trim() ? (
                <Typography.Text code copyable style={{ fontSize: 11 }}>
                  {v.trim()}
                </Typography.Text>
              ) : (
                '—'
              ),
          },
          {
            title: t('common.categories.table.price'),
            dataIndex: 'price',
            key: 'price',
            width: 88,
            align: 'right' as const,
            render: (v: number) => `€${Number(v).toFixed(2)}`,
          },
          {
            title: t('common.categories.table.stock'),
            dataIndex: 'stockQuantity',
            key: 'stock',
            width: 72,
            align: 'right' as const,
          },
        ]}
      />
    </div>
  );
}

export default function CategoriesPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const { user } = useAuth();
  const canManageCategories = hasPermission(user, PERMISSIONS.CATEGORY_MANAGE);
  const [searchTerm, setSearchTerm] = useState('');
  const [searchDebounced, setSearchDebounced] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setSearchDebounced(searchTerm), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const { useList, useSearch, useCreate, useUpdate, useDelete, invalidateList } = useCategories();

  const listOptions: Partial<UseQueryOptions<AdminCategory[], Error, AdminCategory[]>> = {
    placeholderData: keepPreviousData,
  };
  const listQuery = useList(listOptions);
  const searchQuery = useSearch(searchDebounced.trim(), listOptions);

  const isSearching = searchDebounced.trim().length > 0;
  const activeQuery = isSearching ? searchQuery : listQuery;
  const categories = (isSearching ? searchQuery.data : listQuery.data) ?? [];
  const isLoading = isSearching ? searchQuery.isLoading : listQuery.isLoading;
  const isError = activeQuery.isError;
  const error = activeQuery.error;
  const refetch = activeQuery.refetch;

  const createMutation = useCreate();
  const updateMutation = useUpdate();
  const deleteMutation = useDelete();

  const [formVisible, setFormVisible] = useState(false);
  const [editingCategory, setEditingCategory] = useState<AdminCategory | null>(null);

  const handleCreate = async (values: CategoryFormSubmitValues) => {
    try {
      const taxRate = values.defaultTaxRate ?? values.vatRate ?? 20;
      await createMutation.mutateAsync({
        data: {
          name: values.name,
          sortOrder: values.sortOrder ?? 0,
          defaultTaxRate: taxRate,
          vatRate: taxRate,
        },
      });
      message.success(t('common.categories.messages.created'));
      setFormVisible(false);
      invalidateList();
    } catch {
      message.error(t('common.categories.messages.createError'));
    }
  };

  const handleUpdate = async (values: CategoryFormSubmitValues) => {
    if (!editingCategory?.id) return;
    try {
      const taxRate = values.defaultTaxRate ?? values.vatRate ?? categoryTaxRate(editingCategory);
      await updateMutation.mutateAsync({
        id: editingCategory.id,
        data: buildCategoryUpdatePayload(editingCategory, {
          name: values.name,
          sortOrder: values.sortOrder ?? 0,
          defaultTaxRate: taxRate,
        }),
      });
      message.success(t('common.categories.messages.updated'));
      setFormVisible(false);
      setEditingCategory(null);
      invalidateList();
    } catch {
      message.error(t('common.categories.messages.updateError'));
    }
  };

  const handleInlineNameUpdate = async (category: AdminCategory, newName: string) => {
    if (!category.id) return;
    try {
      await updateMutation.mutateAsync({
        id: category.id,
        data: buildCategoryUpdatePayload(category, { name: newName }),
      });
      message.success(t('common.categories.messages.updated'));
      invalidateList();
    } catch {
      message.error(t('common.categories.messages.updateError'));
      throw new Error('Category name update failed');
    }
  };

  const handleDelete = async (category: AdminCategory) => {
    if (!category.id) return;
    try {
      await deleteMutation.mutateAsync({ id: category.id });
      message.success(t('common.categories.messages.deleted'));
      invalidateList();
    } catch {
      message.error(t('common.categories.messages.deleteError'));
    }
  };

  const openCreate = () => {
    setEditingCategory(null);
    setFormVisible(true);
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t('nav.categories')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('nav.categories') }]}
        actions={
          <Flex wrap="wrap" gap="middle" align="center" justify="flex-end">
            <Input.Search
              placeholder={t('common.categories.searchPlaceholder')}
              allowClear
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onSearch={(v) => setSearchTerm(v)}
              style={{ width: 280, maxWidth: '100%' }}
            />
            {canManageCategories ? <ResetCategoriesButton /> : null}
            {canManageCategories ? (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('common.categories.newCategory')}
              </Button>
            ) : null}
          </Flex>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('common.categories.searchHint')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {isError ? (
        <Alert
          type="error"
          title={t('common.categories.loadErrorTitle')}
          description={
            error ? (
              <ApiErrorAlertDescription
                t={t}
                error={error}
                logContext="CategoriesPage"
                fallbackKey="common.messages.unknownError"
              />
            ) : (
              t('common.messages.unknownError')
            )
          }
          showIcon
          action={
            <Button size="small" onClick={() => refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : null}

      {!isError ? (
        <CategoryTable
          data={categories}
          loading={isLoading}
          canManage={canManageCategories}
          onEdit={(record) => {
            setEditingCategory(record);
            setFormVisible(true);
          }}
          onDelete={handleDelete}
          onUpdateName={handleInlineNameUpdate}
          deleteLoadingId={deleteMutation.isPending ? deleteMutation.variables?.id : undefined}
          expandable={{
            expandedRowRender: (record) =>
              record.id ? <CategoryProducts categoryId={record.id} /> : null,
            rowExpandable: () => true,
          }}
        />
      ) : null}

      <CategoryForm
        visible={formVisible}
        initialValues={editingCategory ?? undefined}
        onCancel={() => {
          setFormVisible(false);
          setEditingCategory(null);
        }}
        onSubmit={editingCategory ? handleUpdate : handleCreate}
        loading={createMutation.isPending || updateMutation.isPending}
      />
    </Space>
  );
}
