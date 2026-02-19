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
    // Remove search from filters, use local state
    const [searchTerm, setSearchTerm] = useState('');
    const [searchDebounced, setSearchDebounced] = useState('');

    // Debounce search term
    React.useEffect(() => {
        const timer = setTimeout(() => {
            setSearchDebounced(searchTerm);
            if (searchTerm) {
                // Reset to page 1 when searching (though pagination is disabled for search results for now)
                setPage(1);
            }
        }, 500);
        return () => clearTimeout(timer);
    }, [searchTerm]);

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
    const isSearching = !!searchDebounced && searchDebounced.length >= 2;

    const listQuery = useList(
        { page, pageSize },
        {
            query: {
                enabled: !isSearching,
                placeholderData: keepPreviousData
            } as any
        }
    );

    const searchQuery = useProducts().useSearch(
        { name: searchDebounced },
        { query: { enabled: isSearching } }
    );

    const isLoading = isSearching ? searchQuery.isLoading : listQuery.isLoading;

    // Extract raw data 
    // Search endpoint returns { data: Product[] } (Array)
    // List endpoint returns { data: { items: Product[], ... } }
    const rawSearchResults =
        (searchQuery.data as any)?.data?.data ??   // AxiosResponse + wrapper: { success, data: [...] }
        (searchQuery.data as any)?.data ??         // wrapper: { success, data: [...] }
        [];
    const rawListItems = listQuery.data?.data?.items || [];

    // Normalize data source
    const rawItems = isSearching ? rawSearchResults : rawListItems;

    // Map to UI model (camelCase)
    // Ensure we handle potential PascalCase fields from backend if not handled by mapper
    const products = Array.isArray(rawItems) ? rawItems.map(mapApiProductToUi) : [];

    const pagination = isSearching
        ? false // Disable pagination for search results as endpoint returns all matches (or we need to implement client-side/search-specific pagination)
        : (listQuery.data?.data?.pagination ? {
            current: page,
            pageSize: pageSize,
            total: listQuery.data.data.pagination.totalCount,
            showSizeChanger: true,
            onChange: (p: number, ps: number) => {
                setPage(p);
                setPageSize(ps);
            },
        } : false);

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
            render: (text: string) => <span style={{ fontWeight: 600 }}>{text}</span>,
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
                        // Remove router dependency: onSearch={(value) => setParam('search', value)}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        onSearch={(value) => {
                            setPage(1);
                            setParam('search', value);
                        }}
                        style={{ width: 300 }}
                        value={searchTerm}
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
                pagination={pagination}
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
