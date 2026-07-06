'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useCallback, useMemo, useState } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { Modal, Button, Table, Space, Tag, Popconfirm, Alert, Empty, InputNumber, Typography, Flex, Tooltip } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, StockOutlined, ClearOutlined } from '@ant-design/icons';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
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
import { useProductStatusCounts } from '@/features/products/hooks/useProductStatusCounts';
import { countActiveProductFilters } from '@/features/products/utils/countActiveProductFilters';
import { DevCatalogPurgeButton } from '@/features/products/components/DevCatalogPurgeButton';
import { isDevelopment } from '@/features/auth/services/devTenant';

const MIN_SEARCH_LENGTH = 2;

/** Slightly de-emphasize inactive product rows without hiding actions */
const INACTIVE_PRODUCT_ROW_STYLE: React.CSSProperties = { opacity: 0.82 };

export default function ProductsPage() {
  const { message, modal } = useAntdApp();

    const showProductLagerUi = isAdminProductsLagerUiEnabled();
    const { t } = useI18n();
    const router = useRouter();
    const searchParams = useSearchParams();
    const { tenantName, tenantId, tenantSlug, isRealTenantSlug, isSuperAdminUser } = useCurrentTenant();
    const { user } = useAuth();
    const canManageProducts = hasPermission(user, PERMISSIONS.PRODUCT_MANAGE);

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
        useDetail,
        useCreate,
        useUpdate,
        useDelete,
        useBulkDeactivate,
        useDeactivateAll,
        useUpdateStock,
        useSetModifierGroups,
        invalidateList,
    } = useProducts();

    const { useList: useCategoriesList, invalidateList: invalidateCategoriesList } = useCategories();
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
    const bulkDeactivateMutation = useBulkDeactivate();
    const deactivateAllMutation = useDeactivateAll();

    const showDevCatalogPurge =
        isDevelopment() && isSuperAdminUser && isRealTenantSlug && canManageProducts;

    const statusCounts = useProductStatusCounts(true);
    const activeProductCount = statusCounts.active;
    const totalProductCount = statusCounts.all;

    const handleCatalogPurgeSuccess = useCallback(() => {
        setSelectedRowKeys([]);
        invalidateList();
        invalidateCategoriesList();
        statusCounts.refetch();
    }, [invalidateCategoriesList, invalidateList, statusCounts]);
    const stockMutation = useUpdateStock();
    const setModifierGroupsMutation = useSetModifierGroups();

    const [formVisible, setFormVisible] = useState(false);
    const [editingProductId, setEditingProductId] = useState<string | null>(null);
    const editDetailQuery = useDetail(editingProductId ?? '', {
        enabled: formVisible && !!editingProductId,
    });
    const [stockModalProduct, setStockModalProduct] = useState<Product | null>(null);
    const [stockQuantity, setStockQuantity] = useState<number>(0);
    const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

    const { data: listData, isLoading, isError, error, refetch } = listQuery;
    const filteredResultCount = listData?.pagination?.totalCount;
    const hasNonStatusFilters = useMemo(
        () => countActiveProductFilters({ ...filters, status: 'active' }) > 0,
        [filters],
    );
    const rawItems = listData?.items ?? [];
    const products = rawItems.map(mapApiProductToUi);
    const editingProductFallback = useMemo(
        () => (editingProductId ? products.find((p) => p.id === editingProductId) ?? null : null),
        [editingProductId, products],
    );
    const editingInitialValues = editingProductId
        ? (editDetailQuery.data ?? editingProductFallback)
        : null;
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
        if (!editingProductId) return;
        try {
            const apiData = mapUiProductToApi(values);
            (apiData as Record<string, unknown>).id = editingProductId;
            const result = await updateMutation.mutateAsync({
                id: editingProductId,
                data: apiData as unknown as Product,
            });
            if (values.modifierGroupIds !== undefined) {
                await setModifierGroupsMutation.mutateAsync({ productId: editingProductId, modifierGroupIds: values.modifierGroupIds });
            }
            message.success(result?.fromPayload ? t('products.messages.updateSuccessRefreshing') : t('products.messages.updateSuccess'));
            setFormVisible(false);
            setEditingProductId(null);
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
            setSelectedRowKeys((prev) => prev.filter((key) => key !== id));
            invalidateList();
        } catch {
            message.error(t('products.messages.deleteError'));
        }
    };

    const selectedActiveCount = useMemo(
        () =>
            products.filter(
                (product) => product.id && selectedRowKeys.includes(product.id) && product.isActive !== false,
            ).length,
        [products, selectedRowKeys],
    );

    const handleBulkDeactivate = useCallback(() => {
        const activeIds = products
            .filter((product) => product.id && selectedRowKeys.includes(product.id) && product.isActive !== false)
            .map((product) => product.id as string);
        if (activeIds.length === 0) return;

        modal.confirm({
            title: t('products.actions.bulkDeactivateTitle'),
            content: t('products.actions.bulkDeactivateDescription', { count: activeIds.length }),
            okText: t('products.actions.bulkDeactivate'),
            okButtonProps: { danger: true },
            cancelText: t('common.buttons.cancel'),
            onOk: async () => {
                try {
                    const result = await bulkDeactivateMutation.mutateAsync({ productIds: activeIds });
                    const skipped = result.alreadyInactive + result.notFound;
                    if (skipped > 0) {
                        message.warning(
                            t('products.actions.bulkDeactivatePartial', {
                                deactivated: result.deactivated,
                                skipped,
                            }),
                        );
                    } else {
                        message.success(
                            t('products.actions.bulkDeactivateSuccess', { count: result.deactivated }),
                        );
                    }
                    setSelectedRowKeys([]);
                    invalidateList();
                } catch {
                    message.error(t('products.actions.bulkDeactivateError'));
                }
            },
        });
    }, [bulkDeactivateMutation, invalidateList, message, modal, products, selectedRowKeys, t]);

    const handleDeactivateAllCatalog = useCallback(() => {
        if (activeProductCount === 0) {
            message.info(t('products.actions.deactivateAllNone'));
            return;
        }

        modal.confirm({
            title: t('products.actions.deactivateAllTitle'),
            content: t('products.actions.deactivateAllDescription', { count: activeProductCount }),
            okText: t('products.actions.deactivateAllCatalog'),
            okButtonProps: { danger: true },
            cancelText: t('common.buttons.cancel'),
            onOk: async () => {
                try {
                    const result = await deactivateAllMutation.mutateAsync();
                    if (result.deactivated > 0) {
                        message.success(
                            t('products.actions.deactivateAllSuccess', { count: result.deactivated }),
                        );
                    } else {
                        message.info(t('products.actions.deactivateAllNone'));
                    }
                    setSelectedRowKeys([]);
                    invalidateList();
                    statusCounts.refetch();
                } catch {
                    message.error(t('products.actions.deactivateAllError'));
                }
            },
        });
    }, [
        activeProductCount,
        deactivateAllMutation,
        invalidateList,
        message,
        modal,
        statusCounts,
        t,
    ]);

    const openCreate = () => {
        setEditingProductId(null);
        setFormVisible(true);
    };

    const openEdit = (product: Product) => {
        if (!product.id) return;
        setEditingProductId(product.id);
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
                    <Space orientation="vertical" size={0}>
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
                    <Space orientation="vertical" size={2} style={{ width: '100%', maxWidth: 320 }}>
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
                        <Space orientation="vertical" size={0} style={{ textAlign: 'right', width: '100%' }}>
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
                    {record.isActive !== false ? (
                        <Popconfirm
                            title={t('products.actions.deleteConfirmTitle')}
                            description={t('products.actions.deleteConfirmDescription')}
                            onConfirm={() => record.id && handleDelete(record.id)}
                            okText={t('common.buttons.yes')}
                            cancelText={t('common.buttons.no')}
                        >
                            <Button
                                type="default"
                                size="small"
                                danger
                                icon={<DeleteOutlined />}
                                loading={deleteMutation.isPending}
                            >
                                {t('products.actions.delete')}
                            </Button>
                        </Popconfirm>
                    ) : null}
                </Space>
            ),
        },
    ];

    return (
        <Space orientation="vertical" size="large" style={{ width: '100%' }}>
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
                        {showDevCatalogPurge ? (
                            <DevCatalogPurgeButton
                                tenantSlug={tenantSlug ?? 'dev'}
                                tenantId={tenantId ?? undefined}
                                productCount={totalProductCount}
                                categoryCount={categories.length}
                                loading={statusCounts.isLoading || categoriesQuery.isLoading}
                                onSuccess={handleCatalogPurgeSuccess}
                            />
                        ) : null}
                        {canManageProducts ? (
                            <Button
                                danger
                                icon={<ClearOutlined />}
                                disabled={activeProductCount === 0}
                                loading={deactivateAllMutation.isPending || statusCounts.isLoading}
                                onClick={handleDeactivateAllCatalog}
                            >
                                {t('products.actions.deactivateAllCatalog')}
                            </Button>
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
                statusCounts={statusCounts}
                filteredResultCount={
                    hasNonStatusFilters || (filters.searchTerm?.trim().length ?? 0) >= MIN_SEARCH_LENGTH
                        ? filteredResultCount
                        : undefined
                }
            />

            {isError ? (
                <Alert
                    type="error"
                    title={t('products.page.loadErrorTitle')}
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

            {!isError && selectedRowKeys.length > 0 ? (
                <Alert
                    type="info"
                    showIcon
                    title={t('products.actions.bulkSelected', { count: selectedRowKeys.length })}
                    action={
                        <Space>
                            <Button size="small" onClick={() => setSelectedRowKeys([])}>
                                {t('products.actions.clearSelection')}
                            </Button>
                            <Button
                                size="small"
                                danger
                                icon={<DeleteOutlined />}
                                disabled={selectedActiveCount === 0}
                                loading={bulkDeactivateMutation.isPending}
                                onClick={handleBulkDeactivate}
                            >
                                {t('products.actions.bulkDeactivate')}
                            </Button>
                        </Space>
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
                    rowSelection={{
                        selectedRowKeys,
                        onChange: (keys) => setSelectedRowKeys(keys),
                        getCheckboxProps: (record) => ({
                            disabled: record.isActive === false,
                        }),
                    }}
                    onRow={(record) =>
                        record.isActive === false
                            ? { style: INACTIVE_PRODUCT_ROW_STYLE }
                            : {}
                    }
                />
            ) : null}

            <ProductForm
                visible={formVisible}
                initialValues={editingInitialValues}
                isEditMode={!!editingProductId}
                onCancel={() => { setFormVisible(false); setEditingProductId(null); }}
                onSubmit={editingProductId ? handleUpdate : handleCreate}
                loading={
                    createMutation.isPending
                    || updateMutation.isPending
                    || (!!editingProductId && editDetailQuery.isFetching)
                }
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
