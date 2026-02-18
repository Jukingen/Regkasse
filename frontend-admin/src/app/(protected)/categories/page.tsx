'use client';

import React, { useState } from 'react';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Tooltip, Empty, Spin } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';
import { useCategories } from '@/features/categories/hooks/useCategories';
import { Category, Product, CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';
import CategoryForm from '@/features/categories/components/CategoryForm';
import { ColumnType } from 'antd/es/table';

// Sub-component for rendering products within a category
function CategoryProducts({ categoryId }: { categoryId: string }) {
    const { useProductsByCategory } = useCategories();
    const { data: products, isLoading, isError } = useProductsByCategory(categoryId);

    if (isLoading) return <div style={{ padding: 16, textAlign: 'center' }}><Spin /></div>;
    if (isError) return <div style={{ color: 'red', padding: 16 }}>Failed to load products</div>;
    if (!products || products.length === 0) return <Empty description="No products in this category" image={Empty.PRESENTED_IMAGE_SIMPLE} />;

    const columns: ColumnType<Product>[] = [
        { title: 'Product Name', dataIndex: 'name', key: 'name' },
        { title: 'Barcode', dataIndex: 'barcode', key: 'barcode' },
        { title: 'Price', dataIndex: 'price', key: 'price', render: (val) => `€${val?.toFixed(2)}` },
        { title: 'Stock', dataIndex: 'stockQuantity', key: 'stock' },
    ];

    return (
        <Table
            columns={columns}
            dataSource={products}
            rowKey="id"
            pagination={false}
            size="small"
            style={{ margin: 16 }}
        />
    );
}

export default function CategoriesPage() {
    const [search, setSearch] = useState('');
    const { useList, useCreate, useUpdate, useDelete, invalidateList } = useCategories();

    // Query
    const { data: categories, isLoading } = useList();

    // Filtered data (client-side)
    const filteredCategories = React.useMemo(() => {
        if (!categories) return [];
        if (!search) return categories;
        const lower = search.toLowerCase();
        return categories.filter(c =>
            c.name.toLowerCase().includes(lower) ||
            c.description?.toLowerCase().includes(lower)
        );
    }, [categories, search]);

    // Mutations
    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    // Modal State
    const [isFormVisible, setIsFormVisible] = useState(false);
    const [editingCategory, setEditingCategory] = useState<Category | null>(null);

    // Handlers
    const handleCreate = async (values: CreateCategoryRequest) => {
        try {
            await createMutation.mutateAsync({ data: values });
            message.success('Category created successfully');
            setIsFormVisible(false);
            invalidateList();
        } catch (error) {
            message.error('Failed to create category');
        }
    };

    const handleUpdate = async (values: UpdateCategoryRequest) => {
        if (!editingCategory?.id) return;
        try {
            await updateMutation.mutateAsync({ id: editingCategory.id, data: values });
            message.success('Category updated successfully');
            setIsFormVisible(false);
            setEditingCategory(null);
            invalidateList();
        } catch (error) {
            message.error('Failed to update category');
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Category deleted successfully');
            invalidateList();
        } catch (error) {
            message.error('Failed to delete category');
        }
    };

    const openCreate = () => {
        setEditingCategory(null);
        setIsFormVisible(true);
    };

    const openEdit = (cat: Category) => {
        setEditingCategory(cat);
        setIsFormVisible(true);
    };

    const columns: ColumnType<Category>[] = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (text, record) => (
                <Space>
                    {record.color && (
                        <div style={{ width: 12, height: 12, borderRadius: '50%', background: record.color, border: '1px solid #ddd' }} />
                    )}
                    <span style={{ fontWeight: 600 }}>{text}</span>
                </Space>
            )
        },
        { title: 'Description', dataIndex: 'description', key: 'desc', ellipsis: true },
        { title: 'Sort Order', dataIndex: 'sortOrder', key: 'sort', sorter: (a, b) => (a.sortOrder || 0) - (b.sortOrder || 0) },
        {
            title: 'Actions',
            key: 'actions',
            align: 'right',
            render: (_, record) => (
                <Space>
                    <Tooltip title="Edit">
                        <Button icon={<EditOutlined />} onClick={() => openEdit(record)} />
                    </Tooltip>
                    <Popconfirm
                        title="Delete category?"
                        description="This might affect products linked to this category."
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Button danger icon={<DeleteOutlined />} loading={deleteMutation.isPending && deleteMutation.variables?.id === record.id} />
                    </Popconfirm>
                </Space>
            )
        }
    ];

    return (
        <div style={{ padding: 24, background: '#fff', borderRadius: 8 }}>
            <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between' }}>
                <Input.Search
                    placeholder="Search categories..."
                    allowClear
                    onSearch={setSearch}
                    onChange={(e) => setSearch(e.target.value)}
                    style={{ width: 300 }}
                />
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                    New Category
                </Button>
            </div>

            <Table
                columns={columns}
                dataSource={filteredCategories}
                rowKey="id"
                loading={isLoading}
                expandable={{
                    expandedRowRender: (record) => record.id ? <CategoryProducts categoryId={record.id} /> : null,
                    rowExpandable: (record) => true,
                }}
                pagination={{ pageSize: 10 }}
            />

            <CategoryForm
                visible={isFormVisible}
                initialValues={editingCategory}
                onCancel={() => setIsFormVisible(false)}
                // @ts-ignore - union type compatibility check
                onSubmit={editingCategory ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />
        </div>
    );
}
