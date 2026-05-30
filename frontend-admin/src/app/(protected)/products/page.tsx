'use client';

import React, { useCallback, useMemo, useState } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Button, Table, Space, message, Tag, Popconfirm, Alert, Empty, Modal, InputNumber, Typography, Flex, Tooltip } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined } from '@ant-design/icons';
import { useRouter, useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useProducts } from '@/features/products/hooks/useProducts';
import { ProductFilterBar } from '@/features/products/components/ProductFilterBar';
import type { ProductFilters } from '@/features/products/types/productFilters';
import {
    buildProductListSearchParams,
    parseProductFiltersFromSearchParams,
    parseProductPaginationFromSearchParams,
} from '@/features/products/utils/productFilterUrl';
import { productFiltersToApiParams } from '@/features/products/utils/productFiltersToApiParams';
import { useCategories } from '@/features/categories/hooks/useCategories';
import type { AdminCategory } from '@/features/categories/types';
import { Product } from '@/api/generated/model';
import { mapApiProductToUi, mapUiProductToApi, formatTaxTypeLabelForLocale, formatProductUnitLabelForLocale } from '@/features/products/utils/productMapper';
import ProductForm, { type ProductFormSubmitValues } from '@/features/products/components/ProductForm';
import { DemoImportButton } from '@/features/tenants/components/DemoImportButton';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { ColumnType } from 'antd/es/table';
import { useI18n } from '@/i18n';
import { FORMAT_EMPTY_DISPLAY } from '@/i18n/formatting';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { isAdminProductsLagerUiEnabled } from '@/features/products/utils/adminProductsLagerUi';

const MIN_SEARCH_LENGTH = 2;

/** Slightly de-emphasize inactive product rows without hiding actions */
const INACTIVE_PRODUCT_ROW_STYLE: React.CSSProperties = { opacity: 0.82 };

export default function ProductsPage() {
    const showProductLagerUi = isAdminProductsLagerUiEnabled();
    const { t } = useI18n();
    const router = useRouter();
    const searchParams = useSearchParams();
    const { tenantName, tenantId, tenantSlug, isRealTenantSlug } = useCurrentTenant();

    const filters = useMemo(
        () => parseProductFiltersFromSearchParams(new URLSearchParams(searchParams.toString())),
        [searchParams],
    );
    const pagination = useMemo(
        () => parseProductPaginationFromSearchParams(new URLSearchParams(searchParams.toString())),
        [searchParams],
    );

    const {
        useList,
        useCreate,
        useUpdate,
        useDelete,
        useUpdateStock,
        useSetModifierGroups,
        invalidateList,
    } = useProducts();

    const { useList: useCategoriesList } = useCategories();
    const categoriesQuery = useCategoriesList();
    const categories = categoriesQuery.data ?? [];

    const listParams = useMemo(
        () => productFiltersToApiParams(filters, pagination),
        [filters, pagination],
    );

    const listQuery = useList(listParams, { placeholderData: keepPreviousData });

    const filterCategories = useMemo((): AdminCategory[] => {
        const fromApi = listQuery.data?.availableFilters?.categories;
        if (fromApi?.length) {
            return fromApi.map((c) => ({
                id: c.id,
                key: c.id,
                name: c.name,
            }));
        }
        return categories;
    }, [listQuery.data?.availableFilters?.categories, categories]);

    const filterTaxTypes = useMemo(() => {
        const values = listQuery.data?.availableFilters?.taxTypes ?? [1, 2, 3, 4];
        return values.map((value) => ({
            value,
            label:
                value === 1
                    ? t('products.form.taxStandard')
                    : value === 2
                      ? t('products.form.taxReduced')
                      : value === 3
                        ? t('products.form.taxSpecial')
                        : t('products.filters.taxZero'),
        }));
    }, [listQuery.data?.availableFilters?.taxTypes, t]);

    const applyFiltersAndPagination = useCallback(
        (nextFilters: ProductFilters, nextPagination: { page: number; pageSize: number }) => {
            const params = buildProductListSearchParams(
                nextFilters,
                nextPagination,
                new URLSearchParams(searchParams.toString()),
            );
            router.replace(`?${params.toString()}`, { scroll: false });
        },
        [router, searchParams],
    );

    const handleFilterChange = useCallback(
        (nextFilters: ProductFilters) => {
            applyFiltersAndPagination(nextFilters, { page: 1, pageSize: pagination.pageSize });
        },
        [applyFiltersAndPagination, pagination.pageSize],
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
    const tablePagination = listData?.pagination
        ? {
            current: pagination.page,
            pageSize: pagination.pageSize,
            total: listData.pagination.totalCount,
            showSizeChanger: true,
            onChange: (p: number, ps: number) => {
                applyFiltersAndPagination(filters, { page: p, pageSize: ps });
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
                    <Flex wrap="wrap" gap="middle" align="center" justify="flex-end">
                        {isRealTenantSlug && tenantName ? (
                            <DemoImportButton
                                tenantId={tenantId ?? undefined}
                                tenantName={tenantName}
                                tenantSlug={tenantSlug}
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

            <ProductFilterBar
                filters={filters}
                onFilterChange={handleFilterChange}
                categories={filterCategories}
                taxTypes={filterTaxTypes}
            />

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
                    pagination={tablePagination}
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
