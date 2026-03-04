'use client';

import React, { useState } from 'react';
import { Button, Table, Space, message, Popconfirm, Tooltip, Empty, Spin, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { CategoryWithVat } from '@/features/categories/types';
import type { CategoryFormSubmitValues } from '@/features/categories/components/CategoryForm';
import CategoryForm from '@/features/categories/components/CategoryForm';
import type { ColumnType } from 'antd/es/table';

// Backend DTOs may not include vatRate in generated types yet; payload we send
type CategoryCreatePayload = Parameters<ReturnType<typeof useCategories>['useCreate']>['0']['data'] & { vatRate?: number };
type CategoryUpdatePayload = Parameters<ReturnType<typeof useCategories>['useUpdate']>['0']['data'] & { vatRate?: number };

function CategoryProducts({ categoryId }: { categoryId: string }) {
    const { useProductsByCategory } = useCategories();
    const { data: products, isLoading, isError } = useProductsByCategory(categoryId);

    if (isLoading) return <div style={{ padding: 16, textAlign: 'center' }}><Spin /></div>;
    if (isError) return <div style={{ color: 'red', padding: 16 }}>Failed to load products</div>;
    if (!products?.length) return <Empty description="No products in this category" image={Empty.PRESENTED_IMAGE_SIMPLE} />;

    return (
        <Table
            size="small"
            dataSource={products}
            rowKey="id"
            pagination={false}
            style={{ margin: 16 }}
            columns={[
                { title: 'Product', dataIndex: 'name', key: 'name' },
                { title: 'Barcode', dataIndex: 'barcode', key: 'barcode' },
                { title: 'Price', dataIndex: 'price', key: 'price', render: (v: number) => `€${v?.toFixed(2)}` },
                { title: 'Stock', dataIndex: 'stockQuantity', key: 'stock' },
            ]}
        />
    );
}

export default function CategoriesPage() {
    const [search, setSearch] = useState('');
    const { useList, useCreate, useUpdate, useDelete, invalidateList } = useCategories();

    const { data: categories, isLoading } = useList();
    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    const [isFormVisible, setIsFormVisible] = useState(false);
    const [editingCategory, setEditingCategory] = useState<CategoryWithVat | null>(null);

    const filteredCategories = React.useMemo(() => {
        if (!categories) return [];
        const list = categories as CategoryWithVat[];
        if (!search.trim()) return list;
        const lower = search.toLowerCase();
        return list.filter(
            (c) =>
                c.name?.toLowerCase().includes(lower) ||
                (c.description ?? '').toLowerCase().includes(lower)
        );
    }, [categories, search]);

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
            setIsFormVisible(false);
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
            setIsFormVisible(false);
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
            render: (_, record) => (
                <Space>
                    <Tooltip title="Edit">
                        <Button
                            icon={<EditOutlined />}
                            onClick={() => {
                                setEditingCategory(record);
                                setIsFormVisible(true);
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
                        <Button
                            danger
                            icon={<DeleteOutlined />}
                            loading={deleteMutation.isPending && deleteMutation.variables?.id === record.id}
                        />
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
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    onSearch={setSearch}
                    style={{ width: 280, maxWidth: '100%' }}
                />
                <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingCategory(null); setIsFormVisible(true); }}>
                    New Category
                </Button>
            </div>

            <Table
                columns={columns}
                dataSource={filteredCategories}
                rowKey="id"
                loading={isLoading}
                pagination={{ pageSize: 10 }}
                expandable={{
                    expandedRowRender: (record) => record.id ? <CategoryProducts categoryId={record.id} /> : null,
                    rowExpandable: () => true,
                }}
            />

            <CategoryForm
                visible={isFormVisible}
                initialValues={editingCategory ?? undefined}
                onCancel={() => { setIsFormVisible(false); setEditingCategory(null); }}
                onSubmit={editingCategory ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />
        </div>
    );
}
