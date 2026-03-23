'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Alert, Empty, Modal, InputNumber, Typography, Flex, Tooltip } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';
import { useProducts, useProductFilters } from '@/features/products/hooks/useProducts';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi, taxTypeToLabel } from '@/features/products/utils/productMapper';
import ProductForm, { type ProductFormSubmitValues } from '@/features/products/components/ProductForm';
import { ColumnType } from 'antd/es/table';
import { useI18n } from '@/i18n/I18nProvider';

const SEARCH_DEBOUNCE_MS = 400;
const MIN_SEARCH_LENGTH = 2;

export default function ProductsPage() {
    const { t } = useI18n();
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
            message.success(t('products.messages.createSuccess'));
            setFormVisible(false);
            invalidateList();
        } catch (err) {
            message.error(t('products.messages.createError'));
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
            message.success(result?.fromPayload ? t('products.messages.updateSuccessRefreshing') : t('products.messages.updateSuccess'));
            setFormVisible(false);
            setEditingProduct(null);
            invalidateList();
        } catch (err) {
            message.error(t('products.messages.updateError'));
            throw err;
        }
    };

    const handleDelete = async (id: string) => {
        try {
            await deleteMutation.mutateAsync({ id });
            message.success(t('products.messages.deleteSuccess'));
            invalidateList();
        } catch {
            message.error(t('products.messages.deleteError'));
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
            message.success(t('products.messages.stockUpdateSuccess'));
            setStockModalProduct(null);
            invalidateList();
        } catch {
            message.error(t('products.messages.stockUpdateError'));
        }
    };

    const columns: ColumnType<Product>[] = [
        {
            title: t('products.table.product'),
            key: 'product',
            ellipsis: true,
            width: 260,
            render: (_: unknown, record: Product) => {
                const desc = record.description?.trim();
                const bc = record.barcode?.trim();
                const rksv = record.rksvProductType?.trim();
                const taxExempt = record.taxExemptionReason?.trim();
                const tipLines: string[] = [];
                if (desc) tipLines.push(desc);
                if (taxExempt) tipLines.push(`${t('products.table.taxExemption')}: ${taxExempt}`);
                const tipExtra =
                    tipLines.length > 0 ? (
                        <div style={{ whiteSpace: 'pre-wrap', maxWidth: 400 }}>{tipLines.join('\n\n')}</div>
                    ) : undefined;
                const cell = (
                    <Space direction="vertical" size={2} style={{ width: '100%', maxWidth: 320 }}>
                        <Typography.Text strong ellipsis style={{ display: 'block' }}>
                            {record.name || '—'}
                        </Typography.Text>
                        {bc ? (
                            <Typography.Text
                                code
                                copyable={{ text: bc }}
                                ellipsis
                                style={{ display: 'block', fontSize: 11, maxWidth: '100%' }}
                            >
                                {bc}
                            </Typography.Text>
                        ) : null}
                        {rksv ? (
                            <Typography.Text type="secondary" ellipsis style={{ display: 'block', fontSize: 11 }}>
                                {t('products.table.rksvLabel')}: {rksv}
                            </Typography.Text>
                        ) : null}
                    </Space>
                );
                return tipExtra ? <Tooltip title={tipExtra}>{cell}</Tooltip> : cell;
            },
        },
        {
            title: t('products.table.price'),
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
            title: t('products.table.stock'),
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            width: 120,
            render: (qty: number, record: Product) => {
                const min = Number(record.minStockLevel) ?? 0;
                const isLow = Number(qty) <= min;
                const unit = record.unit || t('products.table.unitPieces');
                const tag = (
                    <Tag color={isLow ? 'red' : 'green'} style={{ marginInlineEnd: 0 }}>
                        {Number(qty)} {unit}
                    </Tag>
                );
                return (
                    <Space direction="vertical" size={0}>
                        {tag}
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                            {t('products.table.minLabel')}: {min} {unit}
                            {isLow && min > 0 ? ` · ${t('products.table.minReached')}` : ''}
                        </Typography.Text>
                    </Space>
                );
            },
        },
        {
            title: t('products.table.status'),
            dataIndex: 'isActive',
            key: 'isActive',
            width: 96,
            render: (isActive: boolean) => (
                <Tag color={isActive ? 'blue' : 'default'}>
                    {isActive ? t('products.table.active') : t('products.table.inactive')}
                </Tag>
            ),
        },
        {
            title: t('products.table.category'),
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
            title: t('products.table.tax'),
            key: 'tax',
            width: 120,
            align: 'right',
            render: (_: unknown, record: Product) => {
                const rate = Number(record.taxRate ?? 0);
                const label = taxTypeToLabel(Number(record.taxType ?? 1));
                const short = `${rate}%`;
                const labelShort = label.length > 22 ? `${label.slice(0, 20)}…` : label;
                return (
                    <Tooltip title={label}>
                        <Space direction="vertical" size={0} style={{ textAlign: 'right', width: '100%' }}>
                            <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
                                {short}
                            </Typography.Text>
                            <Typography.Text type="secondary" style={{ fontSize: 10, display: 'block' }}>
                                {labelShort}
                            </Typography.Text>
                        </Space>
                    </Tooltip>
                );
            },
        },
        {
            title: t('products.table.actions'),
            key: 'actions',
            width: 220,
            fixed: 'right',
            align: 'right',
            render: (_: unknown, record: Product) => (
                <Space size="small" wrap>
                    <Button type="primary" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
                            {t('products.actions.edit')}
                    </Button>
                    <Button type="default" size="small" icon={<StockOutlined />} onClick={() => openStockModal(record)}>
                            {t('products.actions.stock')}
                    </Button>
                    <Popconfirm
                        title={t('products.actions.deleteConfirmTitle')}
                        description={t('products.actions.deleteConfirmDescription')}
                        onConfirm={() => record.id && handleDelete(record.id)}
                        okText={t('common.buttons.yes')}
                        cancelText={t('common.buttons.no')}
                    >
                        <Button type="default" size="small" danger icon={<DeleteOutlined />} loading={deleteMutation.isPending}>
                            {t('products.actions.delete')}
                        </Button>
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
            <AdminPageHeader
                title={t('products.page.title')}
                breadcrumbs={[adminOverviewCrumb(t), { title: t('products.page.title') }]}
                actions={
                    <Flex wrap="wrap" gap="middle" align="center" justify="flex-end">
                        <Input.Search
                            placeholder={t('products.page.searchPlaceholder')}
                            allowClear
                            onChange={(e) => setSearchTerm(e.target.value)}
                            onSearch={(v) => setSearchTerm(v)}
                            style={{ width: 280, maxWidth: '100%' }}
                            value={searchTerm}
                        />
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            {t('products.page.newProduct')}
                        </Button>
                    </Flex>
                }
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    {t('products.page.searchHint', { min: MIN_SEARCH_LENGTH })}
                </Typography.Paragraph>
            </AdminPageHeader>

            {isError ? (
                <Alert
                    type="error"
                    message={t('products.page.loadErrorTitle')}
                    description={error instanceof Error ? error.message : t('common.messages.unknownError')}
                    showIcon
                    action={
                        <Button size="small" onClick={() => refetch()}>
                            {t('common.buttons.retry')}
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
                    locale={{ emptyText: <Empty description={t('products.page.empty')} /> }}
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
                title={t('products.stockModal.title')}
                open={!!stockModalProduct}
                onOk={handleStockSave}
                onCancel={() => setStockModalProduct(null)}
                confirmLoading={stockMutation.isPending}
                okText={t('common.buttons.save')}
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
