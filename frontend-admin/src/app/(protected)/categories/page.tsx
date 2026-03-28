'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import type { UseQueryOptions } from '@tanstack/react-query';
import type { Category, CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';
import { Button, Table, Space, message, Popconfirm, Empty, Spin, Input, Alert, Typography, Flex } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { CategoryWithVat } from '@/features/categories/types';
import type { CategoryFormSubmitValues } from '@/features/categories/components/CategoryForm';
import CategoryForm from '@/features/categories/components/CategoryForm';
import type { ColumnType } from 'antd/es/table';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

type CategoryCreatePayload = CreateCategoryRequest & { vatRate?: number };
type CategoryUpdatePayload = UpdateCategoryRequest & { vatRate?: number };

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
                    message={t('common.categories.productsLoadError')}
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
                    action={<Button size="small" onClick={() => refetch()}>{t('common.buttons.retry')}</Button>}
                />
            </div>
        );
    }
    if (!products?.length) {
        return (
            <div style={{ padding: 16 }}>
                <Empty description={t('common.categories.emptyCategoryProducts')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
            </div>
        );
    }

    return (
        <div style={{ padding: '8px 16px 16px' }}>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
                {t('common.categories.directlyAssignedProducts')}{' '}
                <Typography.Text strong>{products.length}</Typography.Text>{' '}
                {products.length === 1 ? t('common.categories.productSingular') : t('common.categories.productPlural')}
            </Typography.Text>
            <Table
                size="small"
                dataSource={products}
                rowKey="id"
                pagination={false}
                columns={[
                    { title: t('common.categories.table.product'), dataIndex: 'name', key: 'name', ellipsis: true },
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
    const { t } = useI18n();
    const [searchTerm, setSearchTerm] = useState('');
    const [searchDebounced, setSearchDebounced] = useState('');

    useEffect(() => {
        const timer = setTimeout(() => setSearchDebounced(searchTerm), SEARCH_DEBOUNCE_MS);
        return () => clearTimeout(timer);
    }, [searchTerm]);

    const { useList, useSearch, useCreate, useUpdate, useDelete, invalidateList } = useCategories();

    const listOptions: Partial<UseQueryOptions<Category[], Error, Category[]>> = { placeholderData: keepPreviousData };
    const listQuery = useList(listOptions);
    const searchQuery = useSearch(searchDebounced.trim(), listOptions);

    const isSearching = searchDebounced.trim().length > 0;
    const activeQuery = isSearching ? searchQuery : listQuery;
    const categories = (isSearching ? searchQuery.data : listQuery.data) ?? undefined;
    const isLoading = isSearching ? searchQuery.isLoading : listQuery.isLoading;
    const isError = activeQuery.isError;
    const error = activeQuery.error;
    const refetch = activeQuery.refetch;

    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    const [formVisible, setFormVisible] = useState(false);
    const [editingCategory, setEditingCategory] = useState<CategoryWithVat | null>(null);

    const listForTable = (categories ?? []) as CategoryWithVat[];

    const handleCreate = async (values: CategoryFormSubmitValues) => {
        try {
            await createMutation.mutateAsync({
                data: {
                    name: values.name,
                    sortOrder: values.sortOrder ?? 0,
                    vatRate: values.vatRate ?? 20,
                } as CategoryCreatePayload,
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
            await updateMutation.mutateAsync({
                id: editingCategory.id,
                data: {
                    name: values.name,
                    sortOrder: values.sortOrder ?? 0,
                    vatRate: values.vatRate ?? 20,
                } as CategoryUpdatePayload,
            });
            message.success(t('common.categories.messages.updated'));
            setFormVisible(false);
            setEditingCategory(null);
            invalidateList();
        } catch {
            message.error(t('common.categories.messages.updateError'));
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success(t('common.categories.messages.deleted'));
            invalidateList();
        } catch {
            message.error(t('common.categories.messages.deleteError'));
        }
    };

    const columns: ColumnType<CategoryWithVat>[] = [
        {
            title: t('common.categories.table.name'),
            dataIndex: 'name',
            key: 'name',
            render: (name: string) => <span style={{ fontWeight: 600 }}>{name}</span>,
        },
        {
            title: t('common.categories.table.vatRate'),
            dataIndex: 'vatRate',
            key: 'vatRate',
            align: 'right',
            sorter: (a, b) => (a.vatRate ?? 0) - (b.vatRate ?? 0),
            render: (vat: number | undefined) => (vat != null ? `${vat}%` : '–'),
        },
        {
            title: t('common.categories.table.active'),
            dataIndex: 'isActive',
            key: 'isActive',
            align: 'center',
            render: (active: boolean | undefined) => (active ? t('common.buttons.yes') : t('common.buttons.no')),
        },
        {
            title: t('common.categories.table.sortOrder'),
            dataIndex: 'sortOrder',
            key: 'sortOrder',
            align: 'right',
            sorter: (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0),
        },
        {
            title: t('common.categories.table.actions'),
            key: 'actions',
            width: 200,
            align: 'right',
            render: (_: unknown, record: CategoryWithVat) => (
                <Space size="small" wrap>
                    <Button
                        type="default"
                        size="small"
                        icon={<EditOutlined />}
                        onClick={() => {
                            setEditingCategory(record);
                            setFormVisible(true);
                        }}
                    >
                        {t('common.buttons.edit')}
                    </Button>
                    <Popconfirm
                        title={t('common.categories.deleteConfirmTitle')}
                        description={t('common.categories.deleteConfirmDescription')}
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText={t('common.buttons.yes')}
                        cancelText={t('common.buttons.no')}
                    >
                        <Button
                            type="default"
                            size="small"
                            danger
                            icon={<DeleteOutlined />}
                            loading={deleteMutation.isPending && deleteMutation.variables?.id === record.id}
                        >
                            {t('common.buttons.delete')}
                        </Button>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    const openCreate = () => {
        setEditingCategory(null);
        setFormVisible(true);
    };

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
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
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            {t('common.categories.newCategory')}
                        </Button>
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
                    message={t('common.categories.loadErrorTitle')}
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
                <Table
                    columns={columns}
                    dataSource={listForTable}
                    rowKey="id"
                    loading={isLoading}
                    pagination={{ pageSize: 10, showSizeChanger: true }}
                    locale={{ emptyText: <Empty description={t('common.categories.emptyCategories')} /> }}
                    expandable={{
                        expandedRowRender: (record) => record.id ? <CategoryProducts categoryId={record.id} /> : null,
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
