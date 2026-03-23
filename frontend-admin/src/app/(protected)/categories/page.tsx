'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import type { UseQueryOptions } from '@tanstack/react-query';
import type { Category, CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';
import { Button, Table, Space, message, Popconfirm, Empty, Spin, Input, Alert, Typography, Flex } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { CategoryWithVat } from '@/features/categories/types';
import type { CategoryFormSubmitValues } from '@/features/categories/components/CategoryForm';
import CategoryForm from '@/features/categories/components/CategoryForm';
import type { ColumnType } from 'antd/es/table';

type CategoryCreatePayload = CreateCategoryRequest & { vatRate?: number };
type CategoryUpdatePayload = UpdateCategoryRequest & { vatRate?: number };

const SEARCH_DEBOUNCE_MS = 400;

function CategoryProducts({ categoryId }: { categoryId: string }) {
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
                    message="Produkte konnten nicht geladen werden"
                    description={error instanceof Error ? error.message : undefined}
                    action={<Button size="small" onClick={() => refetch()}>Erneut versuchen</Button>}
                />
            </div>
        );
    }
    if (!products?.length) {
        return (
            <div style={{ padding: 16 }}>
                <Empty description="Keine Produkte in dieser Kategorie" image={Empty.PRESENTED_IMAGE_SIMPLE} />
            </div>
        );
    }

    return (
        <div style={{ padding: '8px 16px 16px' }}>
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
                Direkt dieser Kategorie zugeordnet:{' '}
                <Typography.Text strong>{products.length}</Typography.Text>{' '}
                {products.length === 1 ? 'Produkt' : 'Produkte'}
            </Typography.Text>
            <Table
                size="small"
                dataSource={products}
                rowKey="id"
                pagination={false}
                columns={[
                    { title: 'Produkt', dataIndex: 'name', key: 'name', ellipsis: true },
                    {
                        title: 'Barcode',
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
                        title: 'Preis',
                        dataIndex: 'price',
                        key: 'price',
                        width: 88,
                        align: 'right' as const,
                        render: (v: number) => `€${Number(v).toFixed(2)}`,
                    },
                    {
                        title: 'Bestand',
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
            message.success('Kategorie angelegt.');
            setFormVisible(false);
            invalidateList();
        } catch {
            message.error('Kategorie konnte nicht angelegt werden.');
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
            message.success('Kategorie aktualisiert.');
            setFormVisible(false);
            setEditingCategory(null);
            invalidateList();
        } catch {
            message.error('Kategorie konnte nicht aktualisiert werden.');
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Kategorie gelöscht.');
            invalidateList();
        } catch {
            message.error('Kategorie konnte nicht gelöscht werden.');
        }
    };

    const columns: ColumnType<CategoryWithVat>[] = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (name: string) => <span style={{ fontWeight: 600 }}>{name}</span>,
        },
        {
            title: 'VAT Rate (%)',
            dataIndex: 'vatRate',
            key: 'vatRate',
            align: 'right',
            sorter: (a, b) => (a.vatRate ?? 0) - (b.vatRate ?? 0),
            render: (vat: number | undefined) => (vat != null ? `${vat}%` : '–'),
        },
        {
            title: 'Active',
            dataIndex: 'isActive',
            key: 'isActive',
            align: 'center',
            render: (active: boolean | undefined) => (active ? 'Yes' : 'No'),
        },
        {
            title: 'Sort Order',
            dataIndex: 'sortOrder',
            key: 'sortOrder',
            align: 'right',
            sorter: (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0),
        },
        {
            title: 'Actions',
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
                        Edit
                    </Button>
                    <Popconfirm
                        title="Delete category?"
                        description="Products linked to this category may be affected."
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Button
                            type="default"
                            size="small"
                            danger
                            icon={<DeleteOutlined />}
                            loading={deleteMutation.isPending && deleteMutation.variables?.id === record.id}
                        >
                            Delete
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
                title={ADMIN_NAV_LABELS.categories}
                breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.categories }]}
                actions={
                    <Flex wrap="wrap" gap="middle" align="center" justify="flex-end">
                        <Input.Search
                            placeholder="Search categories..."
                            allowClear
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            onSearch={(v) => setSearchTerm(v)}
                            style={{ width: 280, maxWidth: '100%' }}
                        />
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            New Category
                        </Button>
                    </Flex>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Search filters categories as you type (short delay before the request runs). Expand a row to see products in that category.
                </Typography.Paragraph>
            </AdminPageHeader>

            {isError ? (
                <Alert
                    type="error"
                    message="Kategorien konnten nicht geladen werden"
                    description={error instanceof Error ? error.message : 'Unknown error'}
                    showIcon
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            {OPERATOR_SHARED_COPY.retryAfterError}
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
                    locale={{ emptyText: <Empty description="No categories" /> }}
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
