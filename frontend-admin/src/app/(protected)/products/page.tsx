'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Tooltip, Alert, Empty, Modal, InputNumber } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined } from '@ant-design/icons';
import { useProducts, useProductFilters } from '@/features/products/hooks/useProducts';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi } from '@/features/products/utils/productMapper';
import ProductForm, { type ProductFormSubmitValues } from '@/features/products/components/ProductForm';
import { ColumnType } from 'antd/es/table';

const SEARCH_DEBOUNCE_MS = 400;
const MIN_SEARCH_LENGTH = 2;

export default function ProductsPage() {
    const { filters } = useProductFilters();
    const [page, setPage] = useState(() => Number(filters.page) || 1);
    const [pageSize, setPageSize] = useState(() => Number(filters.pageSize) || 10);
    const [searchTerm, setSearchTerm] = useState('');
    const [searchDebounced, setSearchDebounced] = useState('');

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchDebounced(searchTerm);
            if (searchTerm.trim()) setPage(1);
        }, SEARCH_DEBOUNCE_MS);
        return () => clearTimeout(timer);
    }, [searchTerm]);

    const {
        useList,
        useCreate,
        useUpdate,
        useDelete,
        useUpdateStock,
        useSetModifierGroups,
        invalidateList,
    } = useProducts();

    const listQuery = useList(
        {
            pageNumber: page,
            pageSize,
            name: searchDebounced.trim().length >= MIN_SEARCH_LENGTH ? searchDebounced.trim() : undefined,
            categoryId: filters.categoryId?.trim() || undefined,
        },
        { placeholderData: keepPreviousData }
    );

    const createMutation = useCreate();
    const updateMutation = useUpdate();
    const deleteMutation = useDelete();
    const stockMutation = useUpdateStock();
    const setModifierGroupsMutation = useSetModifierGroups();

    const [formVisible, setFormVisible] = useState(false);
    const [editingProduct, setEditingProduct] = useState<Product | null>(null);
    const [stockModalProduct, setStockModalProduct] = useState<Product | null>(null);
    const [stockQuantity, setStockQuantity] = useState<number>(0);

    const { data: listData, isLoading, isError, error, refetch } = listQuery;
    const rawItems = listData?.items ?? [];
    const products = rawItems.map(mapApiProductToUi);
    const pagination = listData?.pagination
        ? {
            current: page,
            pageSize,
            total: listData.pagination.totalCount,
            showSizeChanger: true,
            onChange: (p: number, ps: number) => {
                setPage(p);
                setPageSize(ps);
            },
        }
        : false;

    const handleCreate = async (values: ProductFormSubmitValues) => {
        try {
            const apiData = mapUiProductToApi(values);
            const result = await createMutation.mutateAsync({ data: apiData }) as { id?: string };
            const createdId = result?.id;
            if (createdId && values.modifierGroupIds?.length) {
                await setModifierGroupsMutation.mutateAsync({ productId: createdId, modifierGroupIds: values.modifierGroupIds });
            }
            message.success('Product created successfully');
            setFormVisible(false);
            invalidateList();
        } catch (err) {
            message.error('Failed to create product');
            throw err;
        }
    };

    const handleUpdate = async (values: ProductFormSubmitValues) => {
        if (!editingProduct?.id) return;
        try {
            const apiData = mapUiProductToApi(values);
            (apiData as Record<string, unknown>).id = editingProduct.id;
            const result = await updateMutation.mutateAsync({ id: editingProduct.id, data: apiData as Product });
            if (values.modifierGroupIds !== undefined) {
                await setModifierGroupsMutation.mutateAsync({ productId: editingProduct.id, modifierGroupIds: values.modifierGroupIds });
            }
            message.success(result?.fromPayload ? 'Saved. List will refresh.' : 'Product updated successfully');
            setFormVisible(false);
            setEditingProduct(null);
            invalidateList();
        } catch (err) {
            message.error('Failed to update product');
            throw err;
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success('Product deleted successfully');
            invalidateList();
        } catch {
            message.error('Failed to delete product');
        }
    };

    const openCreate = () => {
        setEditingProduct(null);
        setFormVisible(true);
    };

    const openEdit = (product: Product) => {
        setEditingProduct(product);
        setFormVisible(true);
    };

    const openStockModal = (product: Product) => {
        setStockModalProduct(product);
        setStockQuantity(Number(product.stockQuantity) ?? 0);
    };

    const handleStockSave = async () => {
        if (!stockModalProduct?.id) return;
        try {
            await stockMutation.mutateAsync({ id: stockModalProduct.id, data: { quantity: stockQuantity } });
            message.success('Stock updated');
            setStockModalProduct(null);
            invalidateList();
        } catch {
            message.error('Failed to update stock');
        }
    };

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
            render: (price: number) => `€${Number(price).toFixed(2)}`,
        },
        {
            title: 'Stock',
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            render: (qty: number, record: Product) => {
                const min = Number(record.minStockLevel) ?? 0;
                const isLow = Number(qty) <= min;
                return (
                    <Tag color={isLow ? 'red' : 'green'}>
                        {Number(qty)} {record.unit || 'pcs'}
                    </Tag>
                );
            },
        },
        {
            title: 'Tax',
            dataIndex: 'taxRate',
            key: 'taxRate',
            render: (rate: number) => `${Number(rate)}%`,
        },
        {
            title: 'Status',
            dataIndex: 'isActive',
            key: 'isActive',
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'blue' : 'default'}>{isActive ? 'Active' : 'Inactive'}</Tag>
            ),
        },
        {
            title: 'Actions',
            key: 'actions',
            align: 'right',
            render: (_: unknown, record: Product) => (
                <Space>
                    <Tooltip title="Edit">
                        <Button type="text" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)} />
                    </Tooltip>
                    <Tooltip title="Adjust stock">
                        <Button
                            type="text"
                            size="small"
                            icon={<StockOutlined />}
                            onClick={() => openStockModal(record)}
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
                            <Button
                                type="text"
                                size="small"
                                danger
                                icon={<DeleteOutlined />}
                                loading={deleteMutation.isPending}
                            />
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
                        onChange={(e) => setSearchTerm(e.target.value)}
                        onSearch={(v) => setSearchTerm(v)}
                        style={{ width: 300 }}
                        value={searchTerm}
                    />
                </Space>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                    New Product
                </Button>
            </div>

            {isError && (
                <Alert
                    type="error"
                    message="Failed to load products"
                    description={error instanceof Error ? error.message : 'Unknown error'}
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            Retry
                        </Button>
                    }
                    style={{ marginBottom: 16 }}
                />
            )}

            <Table
                columns={columns}
                dataSource={products}
                rowKey="id"
                loading={isLoading}
                pagination={pagination}
                locale={{ emptyText: <Empty description="No products" /> }}
            />

            <ProductForm
                visible={formVisible}
                initialValues={editingProduct}
                onCancel={() => { setFormVisible(false); setEditingProduct(null); }}
                onSubmit={editingProduct ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />

            <Modal
                title="Adjust stock"
                open={!!stockModalProduct}
                onOk={handleStockSave}
                onCancel={() => setStockModalProduct(null)}
                confirmLoading={stockMutation.isPending}
                okText="Save"
            >
                {stockModalProduct && (
                    <div style={{ marginTop: 8 }}>
                        <div style={{ marginBottom: 8 }}>{stockModalProduct.name}</div>
                        <InputNumber
                            min={0}
                            value={stockQuantity}
                            onChange={(v) => setStockQuantity(Number(v) ?? 0)}
                            style={{ width: '100%' }}
                        />
                    </div>
                )}
            </Modal>
        </div>
    );
}
