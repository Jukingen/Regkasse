'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Alert, Empty, Modal, InputNumber, Typography, Flex, Tooltip } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useProducts, useProductFilters } from '@/features/products/hooks/useProducts';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi, taxTypeToLabel } from '@/features/products/utils/productMapper';
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
            const result = await createMutation.mutateAsync({ data: apiData as unknown as Product });
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
            const result = await updateMutation.mutateAsync({
                id: editingProduct.id,
                data: apiData as unknown as Product,
            });
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
            title: 'Product',
            key: 'product',
            ellipsis: true,
            width: 260,
            render: (_: unknown, record: Product) => {
                const desc = record.description?.trim();
                const bc = record.barcode?.trim();
                const rksv = record.rksvProductType?.trim();
                const tipLines: string[] = [];
                if (desc) tipLines.push(desc);
                if (bc) tipLines.push(`Barcode: ${bc}`);
                if (rksv) tipLines.push(`RKSV type: ${rksv}`);
                if (record.taxExemptionReason?.trim()) {
                    tipLines.push(`Tax exemption: ${record.taxExemptionReason.trim()}`);
                }
                const tip =
                    tipLines.length > 0 ? (
                        <div style={{ whiteSpace: 'pre-wrap', maxWidth: 400 }}>{tipLines.join('\n\n')}</div>
                    ) : undefined;
                const cell = (
                    <Space direction="vertical" size={0} style={{ width: '100%', maxWidth: 320 }}>
                        <Typography.Text strong ellipsis style={{ display: 'block' }}>
                            {record.name || '—'}
                        </Typography.Text>
                        {bc ? (
                            <Typography.Text
                                type="secondary"
                                ellipsis
                                style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
                            >
                                {bc}
                            </Typography.Text>
                        ) : null}
                    </Space>
                );
                return tip ? <Tooltip title={tip}>{cell}</Tooltip> : cell;
            },
        },
        {
            title: 'Price',
            dataIndex: 'price',
            key: 'price',
            width: 100,
            align: 'right',
            render: (price: number) => (
                <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                    €{Number(price).toFixed(2)}
                </Typography.Text>
            ),
        },
        {
            title: 'Stock',
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            width: 120,
            render: (qty: number, record: Product) => {
                const min = Number(record.minStockLevel) ?? 0;
                const isLow = Number(qty) <= min;
                const unit = record.unit || 'pcs';
                const tag = (
                    <Tag color={isLow ? 'red' : 'green'} style={{ marginInlineEnd: 0 }}>
                        {Number(qty)} {unit}
                    </Tag>
                );
                return (
                    <Tooltip title={`Min. stock level: ${min} ${unit}. Highlighted when quantity is at or below minimum.`}>
                        {tag}
                    </Tooltip>
                );
            },
        },
        {
            title: 'Status',
            dataIndex: 'isActive',
            key: 'isActive',
            width: 96,
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'blue' : 'default'}>{isActive ? 'Active' : 'Inactive'}</Tag>
            ),
        },
        {
            title: 'Category',
            dataIndex: 'category',
            key: 'category',
            width: 140,
            ellipsis: true,
            render: (text: string) => (
                <Typography.Text type="secondary" ellipsis={{ tooltip: true }}>
                    {text?.trim() || '—'}
                </Typography.Text>
            ),
        },
        {
            title: 'Tax',
            key: 'tax',
            width: 120,
            align: 'right',
            render: (_: unknown, record: Product) => {
                const rate = Number(record.taxRate ?? 0);
                const label = taxTypeToLabel(Number(record.taxType ?? 1));
                const short = `${rate}%`;
                return (
                    <Tooltip title={label}>
                        <Typography.Text type="secondary" style={{ fontVariantNumeric: 'tabular-nums' }}>
                            {short}
                        </Typography.Text>
                    </Tooltip>
                );
            },
        },
        {
            title: 'Actions',
            key: 'actions',
            width: 220,
            fixed: 'right',
            align: 'right',
            render: (_: unknown, record: Product) => (
                <Space size="small" wrap>
                    <Button type="primary" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
                        Edit
                    </Button>
                    <Button type="default" size="small" icon={<StockOutlined />} onClick={() => openStockModal(record)}>
                        Stock
                    </Button>
                    <Popconfirm
                        title="Delete product?"
                        description="This action cannot be undone."
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText="Yes"
                        cancelText="No"
                    >
                        <Button type="default" size="small" danger icon={<DeleteOutlined />} loading={deleteMutation.isPending}>
                            Delete
                        </Button>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title="Products"
                breadcrumbs={[
                    { title: 'Dashboard', href: '/dashboard' },
                    { title: 'Products' },
                ]}
                actions={
                    <Flex wrap="wrap" gap="middle" align="center" justify="flex-end">
                        <Input.Search
                            placeholder="Search products..."
                            allowClear
                            onChange={(e) => setSearchTerm(e.target.value)}
                            onSearch={(v) => setSearchTerm(v)}
                            style={{ width: 280, maxWidth: '100%' }}
                            value={searchTerm}
                        />
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            New Product
                        </Button>
                    </Flex>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Search by product name; at least {MIN_SEARCH_LENGTH} characters trigger a server request.
                </Typography.Paragraph>
            </AdminPageHeader>

            {isError ? (
                <Alert
                    type="error"
                    message="Failed to load products"
                    description={error instanceof Error ? error.message : 'Unknown error'}
                    showIcon
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            Retry
                        </Button>
                    }
                />
            ) : null}

            {!isError ? (
                <Table<Product>
                    columns={columns}
                    dataSource={products}
                    rowKey="id"
                    loading={isLoading}
                    pagination={pagination}
                    size="middle"
                    scroll={{ x: 1100 }}
                    locale={{ emptyText: <Empty description="No products" /> }}
                />
            ) : null}

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
        </Space>
    );
}
