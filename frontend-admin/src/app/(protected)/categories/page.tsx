'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import type { UseQueryOptions } from '@tanstack/react-query';
import type { Category } from '@/api/generated/model';
import { Button, Table, Space, message, Popconfirm, Tooltip, Empty, Spin, Input, Alert } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { CategoryWithVat } from '@/features/categories/types';
import type { CategoryFormSubmitValues } from '@/features/categories/components/CategoryForm';
import CategoryForm from '@/features/categories/components/CategoryForm';
import type { ColumnType } from 'antd/es/table';

type CategoryCreatePayload = Parameters<ReturnType<typeof useCategories>['useCreate']>['0']['data'] & { vatRate?: number };
type CategoryUpdatePayload = Parameters<ReturnType<typeof useCategories>['useUpdate']>['0']['data'] & { vatRate?: number };

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
                    message="Failed to load products"
                    description={error instanceof Error ? error.message : undefined}
                    action={<Button size="small" onClick={() => refetch()}>Retry</Button>}
                />
            </div>
        );
    }
    if (!products?.length) {
        return (
            <div style={{ padding: 16 }}>
                <Empty description="No products in this category" image={Empty.PRESENTED_IMAGE_SIMPLE} />
            </div>
        );
    }

    return (
        <div style={{ padding: '8px 16px 16px' }}>
            <div style={{ marginBottom: 8, fontSize: 12, color: '#666' }}>Products in this category</div>
            <Table
                size="small"
                dataSource={products}
                rowKey="id"
                pagination={false}
                columns={[
                    { title: 'Product', dataIndex: 'name', key: 'name' },
                    { title: 'Barcode', dataIndex: 'barcode', key: 'barcode' },
                    { title: 'Price', dataIndex: 'price', key: 'price', render: (v: number) => `€${Number(v).toFixed(2)}` },
                    { title: 'Stock', dataIndex: 'stockQuantity', key: 'stock' },
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
            message.success('Category created');
            setFormVisible(false);
            invalidateList();
        } catch {
            message.error('Failed to create category');
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
            message.success('Category updated');
            setFormVisible(false);
            setEditingCategory(null);
            invalidateList();
        } catch {
            message.error('Failed to update category');
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Category deleted');
            invalidateList();
        } catch {
            message.error('Failed to delete category');
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
            align: 'right',
            render: (_: unknown, record: CategoryWithVat) => (
                <Space>
                    <Tooltip title="Edit">
                        <Button
                            type="text"
                            size="small"
                            icon={<EditOutlined />}
                            onClick={() => {
                                setEditingCategory(record);
                                setFormVisible(true);
                            }}
                        />
                    </Tooltip>
                    <Popconfirm
                        title="Delete category?"
                        description="Products linked to this category may be affected."
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Tooltip title="Delete">
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
        <div style={{ padding: 24, background: '#fff', borderRadius: 8 }}>
            <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
                <Input.Search
                    placeholder="Search categories..."
                    allowClear
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    onSearch={(v) => setSearchTerm(v)}
                    style={{ width: 280, maxWidth: '100%' }}
                />
                <Button
                    type="primary"
                    icon={<PlusOutlined />}
                    onClick={() => {
                        setEditingCategory(null);
                        setFormVisible(true);
                    }}
                >
                    New Category
                </Button>
            </div>

            {isError && (
                <Alert
                    type="error"
                    message="Failed to load categories"
                    description={error instanceof Error ? error.message : 'Unknown error'}
                    action={<Button size="small" onClick={() => refetch()}>Retry</Button>}
                    style={{ marginBottom: 16 }}
                />
            )}

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
        </div>
    );
}
