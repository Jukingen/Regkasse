'use client';

import React, { useState, useEffect } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Input, Popconfirm, Alert, Empty, Modal, InputNumber, Typography, Flex, Tooltip, Select } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined } from '@ant-design/icons';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useProducts, useProductFilters } from '@/features/products/hooks/useProducts';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi, formatTaxTypeLabelForLocale, formatProductUnitLabelForLocale } from '@/features/products/utils/productMapper';
import ProductForm, { type ProductFormSubmitValues } from '@/features/products/components/ProductForm';
import { DemoImportButton } from '@/features/tenants/components/DemoImportButton';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { ColumnType } from 'antd/es/table';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import {
    activeFilterFromUrl,
    listIsActiveQueryParam,
    shouldClearProductListStatusQueryParam,
    type ProductListActiveFilter,
} from '@/features/products/utils/productListStatusQuery';
import { isAdminProductsLagerUiEnabled } from '@/features/products/utils/adminProductsLagerUi';

const SEARCH_DEBOUNCE_MS = 400;
const MIN_SEARCH_LENGTH = 2;

/** Slightly de-emphasize inactive product rows without hiding actions */
const INACTIVE_PRODUCT_ROW_STYLE: React.CSSProperties = { opacity: 0.82 };

export default function ProductsPage() {
    const showProductLagerUi = isAdminProductsLagerUiEnabled();
    const { t } = useI18n();
    const { tenantName, tenantId, isRealTenantSlug } = useCurrentTenant();
    const { filters, setParam } = useProductFilters();
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

    const activeFilter = activeFilterFromUrl(filters.status);

    // Drop invalid or empty status= so shared links fall back to default active list
    useEffect(() => {
        if (shouldClearProductListStatusQueryParam(filters.status)) {
            setParam('status', null);
        }
    }, [filters.status, setParam]);

    const listQuery = useList(
        {
            pageNumber: page,
            pageSize,
            name: searchDebounced.trim().length >= MIN_SEARCH_LENGTH ? searchDebounced.trim() : undefined,
            categoryId: filters.categoryId?.trim() || undefined,
            isActive: listIsActiveQueryParam(activeFilter),
        },
        { placeholderData: keepPreviousData }
    );

    const handleActiveFilterChange = (value: ProductListActiveFilter) => {
        setPage(1);
        if (value === 'active') {
            setParam('status', null);
        } else if (value === 'all') {
            setParam('status', 'all');
        } else {
            setParam('status', 'inactive');
        }
    };

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

    const stockColumn: ColumnType<Product> = {
            title: t('products.table.stock'),
            dataIndex: 'stockQuantity',
            key: 'stockQuantity',
            width: 120,
            render: (qty: number, record: Product) => {
                const min = Number(record.minStockLevel) ?? 0;
                const isLow = Number(qty) <= min;
                const unit = formatProductUnitLabelForLocale(record.unit, t);
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
                            {record.name?.trim() ? record.name : FORMAT_EMPTY_DISPLAY}
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
        ...(showProductLagerUi ? [stockColumn] : []),
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
                    {text?.trim() ? text : FORMAT_EMPTY_DISPLAY}
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
                const label = formatTaxTypeLabelForLocale(Number(record.taxType ?? 1), record.taxRate, t);
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
            width: showProductLagerUi ? 220 : 160,
            fixed: 'right',
            align: 'right',
            render: (_: unknown, record: Product) => (
                <Space size="small" wrap>
                    <Button type="primary" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
                            {t('products.actions.edit')}
                    </Button>
                    {showProductLagerUi ? (
                        <Button type="default" size="small" icon={<StockOutlined />} onClick={() => openStockModal(record)}>
                            {t('products.actions.stock')}
                        </Button>
                    ) : null}
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
                    <Flex wrap="wrap" gap="middle" align="center" justify="flex-end" style={{ width: '100%' }}>
                        <Flex
                            wrap="wrap"
                            gap={8}
                            align="center"
                            style={{ flex: 1, minWidth: 0, justifyContent: 'flex-end' }}
                        >
                            {/* Toolbar filters: status today; room for category etc. without crowding the table */}
                            <Select<ProductListActiveFilter>
                                size="small"
                                value={activeFilter}
                                onChange={handleActiveFilterChange}
                                options={[
                                    { value: 'all', label: t('products.page.filterAll') },
                                    { value: 'active', label: t('products.page.filterActive') },
                                    { value: 'inactive', label: t('products.page.filterInactive') },
                                ]}
                                style={{ width: 130, maxWidth: '100%' }}
                                aria-label={t('products.page.filterLabel')}
                                title={t('products.page.filterLabel')}
                            />
                            <Input.Search
                                placeholder={t('products.page.searchPlaceholder')}
                                allowClear
                                onChange={(e) => setSearchTerm(e.target.value)}
                                onSearch={(v) => setSearchTerm(v)}
                                style={{ width: 280, maxWidth: '100%', minWidth: 160 }}
                                value={searchTerm}
                            />
                        </Flex>
                        {isRealTenantSlug && tenantName ? (
                            <DemoImportButton
                                tenantId={tenantId ?? undefined}
                                tenantName={tenantName}
                                onSuccess={invalidateList}
                            />
                        ) : null}
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
                    description={
                        error ? (
                            <ApiErrorAlertDescription
                                t={t}
                                error={error}
                                logContext="ProductsPage"
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
                <Table<Product>
                    columns={columns}
                    dataSource={products}
                    rowKey="id"
                    loading={isLoading}
                    pagination={pagination}
                    size="middle"
                    scroll={{ x: showProductLagerUi ? 1100 : 980 }}
                    locale={{ emptyText: <Empty description={t('products.page.empty')} /> }}
                    onRow={(record) =>
                        record.isActive === false
                            ? { style: INACTIVE_PRODUCT_ROW_STYLE }
                            : {}
                    }
                />
            ) : null}

            <ProductForm
                visible={formVisible}
                initialValues={editingProduct}
                onCancel={() => { setFormVisible(false); setEditingProduct(null); }}
                onSubmit={editingProduct ? handleUpdate : handleCreate}
                loading={createMutation.isPending || updateMutation.isPending}
            />

            {showProductLagerUi ? (
                <Modal
                    title={t('products.stockModal.title')}
                    open={!!stockModalProduct}
                    onOk={handleStockSave}
                    onCancel={() => setStockModalProduct(null)}
                    confirmLoading={stockMutation.isPending}
                    okText={t('common.buttons.save')}
                    cancelText={t('common.buttons.cancel')}
                >
                    {stockModalProduct && (
                        <div style={{ marginTop: 8 }}>
                            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                                {t('products.stockModal.productLabel')}
                            </Typography.Text>
                            <Typography.Text strong style={{ display: 'block', marginBottom: 12 }}>
                                {stockModalProduct.name?.trim() ? stockModalProduct.name : FORMAT_EMPTY_DISPLAY}
                            </Typography.Text>
                            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                                {t('products.stockModal.quantityLabel')}
                            </Typography.Text>
                            <InputNumber
                                min={0}
                                value={stockQuantity}
                                onChange={(v) => setStockQuantity(Number(v) ?? 0)}
                                style={{ width: '100%' }}
                            />
                        </div>
                    )}
                </Modal>
            ) : null}
        </Space>
    );
}
