'use client';

import React, { useState } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Tooltip } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, SearchOutlined } from '@ant-design/icons';
import { useProducts, useProductFilters } from '@/features/products/hooks/useProducts';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi } from '@/features/products/utils/productMapper';
import ProductForm from '@/features/products/components/ProductForm';
import { ColumnType } from 'antd/es/table';

export default function ProductsPage() {
    // 1. URL State Management
    const { filters, setParam } = useProductFilters();

    // Local State for immediate UI updates
    const [page, setPage] = useState(Number(filters.page) || 1);
    const [pageSize, setPageSize] = useState(Number(filters.pageSize) || 10);
    const search = filters.search || '';

    // Sync URL changes to local state (for back/forward navigation)
    // Sync URL changes to local state (for back/forward navigation)
    // Commented out to prevent router interaction during pagination
    /* React.useEffect(() => {
        const urlPage = Number(filters.page) || 1;
        const urlPageSize = Number(filters.pageSize) || 10;
        if (urlPage !== page) setPage(urlPage);
        if (urlPageSize !== pageSize) setPageSize(urlPageSize);
    }, [filters.page, filters.pageSize]); */

    // 2. React Query Hooks
    const {
        useList,
        useCreate,
        useUpdate,
        useDelete,
        invalidateList
    } = useProducts();

    // Queries
    const isSearching = !!search;

    const listQuery = useList(
        { page, pageSize },
        {
            query: {
                enabled: !isSearching,
                placeholderData: keepPreviousData // UX improvement
            } as any
        }
    );

    const searchQuery = useProducts().useSearch(
        { name: search },
        { query: { enabled: isSearching } }
    );

    const isLoading = isSearching ? searchQuery.isLoading : listQuery.isLoading;

    // Extract raw data (PascalCase from backend)
    const rawSearchResults = (searchQuery.data as any) || [];
    const rawListItems = listQuery.data?.data?.items || [];
    const rawItems = isSearching ? rawSearchResults : rawListItems;

    // Map to UI model (camelCase)
    const products = Array.isArray(rawItems) ? rawItems.map(mapApiProductToUi) : [];

    const pagination = isSearching
        ? { totalCount: rawSearchResults.length, totalPages: 1 }
        : (listQuery.data?.data?.pagination || { totalCount: 0, totalPages: 0 });

    // Mutations
    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();

    // 3. Local State for Modals
    const [isFormVisible, setIsFormVisible] = useState(false);
    const [editingProduct, setEditingProduct] = useState<Product | null>(null);

    // 4. Handlers
    const handleCreate = async (values: Product) => {
        try {
            const apiData = mapUiProductToApi(values);
            await createMutation.mutateAsync({ data: apiData });
            message.success('Product created successfully');
            setIsFormVisible(false);
            invalidateList();
        } catch (err) {
            message.error('Failed to create product');
            // Re-throw so ProductForm can handle validation errors
            throw err;
        }
    };

    const handleUpdate = async (values: Product) => {
        if (!editingProduct?.id) return;
        try {
            const apiData = mapUiProductToApi(values);
            // Ensure ID is included (though mapper does it, safety check)
            apiData.id = editingProduct.id;

            await updateMutation.mutateAsync({ id: editingProduct.id, data: apiData });
            message.success('Product updated successfully');
            setIsFormVisible(false);
            setEditingProduct(null);
            invalidateList();
        } catch (err) {
            message.error('Failed to update product');
            // Re-throw so ProductForm can handle validation errors
            throw err;
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Product deleted successfully');
            invalidateList();
        } catch (err) {
            message.error('Failed to delete product');
        }
    };

    const openCreateModal = () => {
        setEditingProduct(null);
        setIsFormVisible(true);
    };

    const openEditModal = (product: Product) => {
        setEditingProduct(product);
        setIsFormVisible(true);
    }

    // 5. Table Configuration
    const columns: ColumnType<Product>[] = [
        {
            title: 'Name',
            dataIndex: 'name',
            key: 'name',
            render: (text: string, record: Product) => (
                <Space direction="vertical" size={0}>
                    <span style={{ fontWeight: 600 }}>{text}</span>
                    <span style={{ fontSize: 12, color: '#888' }}>{record.barcode || '-'}</span>
                </Space>
            ),
        },
        {
            title: 'Category',
            dataIndex: 'category',
            key: 'category',
        },
        {
            title: 'Price',
            dataIndex: 'price',
            key: 'price',
            render: (price: number) => `€${price?.toFixed(2)}`,
        },
        {
            title: 'Stock',
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            render: (stock: number, record: Product) => {
                const min = record.minStockLevel || 0;
                const isLow = stock <= min;
                return (
                    <Tag color={isLow ? 'red' : 'green'}>{stock} {record.unit || 'pcs'}</Tag>
                );
            }
        },
        {
            title: 'Tax',
            dataIndex: 'taxRate',
            key: 'taxRate',
            render: (rate: number) => `${rate}%`,
        },
        {
            title: 'Status',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'blue' : 'default'}>{isActive ? 'Active' : 'Inactive'}</Tag>
            )
        },
        {
            title: 'Actions',
            key: 'actions',
            align: 'right',
            render: (_: any, record: Product) => (
                <Space>
                    <Tooltip title="Edit">
                        <Button
                            icon={<EditOutlined />}
                            onClick={() => openEditModal(record)}
                        />
                    </Tooltip>
                    <Popconfirm
                        title="Delete product?"
                        description="This action cannot be undone."
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Tooltip title="Delete">
                            <Button danger icon={<DeleteOutlined />} loading={deleteMutation.isPending} />
                        </Tooltip>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div style={{ padding: 24, background: '#fff', borderRadius: 8 }}>
            <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <Space>
                    <Input.Search
                        placeholder="Search products..."
                        allowClear
                        onSearch={(value) => setParam('search', value)}
                        style={{ width: 300 }}
                        defaultValue={search}
                    />
                </Space>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>
                    New Product
                </Button>
            </div>

            <Table
                columns={columns}
                dataSource={products}
                rowKey="id"
                loading={isLoading}
                pagination={{
                    current: page,
                    pageSize: pageSize,
                    total: pagination.totalCount,
                    showSizeChanger: true,
                    onChange: (p, ps) => {
                        // 1. Update Local State (Trigger Fetch)
                        setPage(p);
                        setPageSize(ps);
                    },
                }}
            />

            <ProductForm
                visible={isFormVisible}
                initialValues={editingProduct}
                onCancel={() => setIsFormVisible(false)}
                onSubmit={editingProduct ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />
        </div>
    );
}
