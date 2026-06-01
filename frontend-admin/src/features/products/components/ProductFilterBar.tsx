'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    Badge,
    Button,
    Card,
    Checkbox,
    Col,
    Collapse,
    DatePicker,
    Drawer,
    Input,
    InputNumber,
    Row,
    Select,
    Slider,
    Space,
    Tag,
} from 'antd';
import {
    ClearOutlined,
    DollarOutlined,
    FilterOutlined,
    SearchOutlined,
    StockOutlined,
    TagOutlined,
} from '@ant-design/icons';
import type { Dayjs } from 'dayjs';
import type { AdminCategory } from '@/features/categories/types';
import type { ProductFilters, ProductListActiveFilter, ProductStockFilterType } from '@/features/products/types/productFilters';
import { countActiveProductFilters } from '@/features/products/utils/countActiveProductFilters';
import { useI18n } from '@/i18n';

const { RangePicker } = DatePicker;

const PRICE_SLIDER_MAX = 200;
const STOCK_STATUS_OPTIONS: ProductStockFilterType[] = ['InStock', 'OutOfStock', 'LowStock', 'Overstock'];

const DEFAULT_TAX_TYPE_VALUES = [1, 2, 3, 4] as const;

export type ProductTaxTypeFilterOption = {
    value: number;
    label: string;
};

export interface ProductFilterBarProps {
    filters: ProductFilters;
    onFilterChange: (filters: ProductFilters) => void;
    categories: AdminCategory[];
    taxTypes?: ProductTaxTypeFilterOption[];
}

type FilterKey = keyof ProductFilters;

function resolveStatusFromCheckboxes(activeChecked: boolean, inactiveChecked: boolean): ProductListActiveFilter {
    if (activeChecked && inactiveChecked) return 'all';
    if (activeChecked) return 'active';
    if (inactiveChecked) return 'inactive';
    return 'all';
}

function statusToCheckboxes(status: ProductListActiveFilter | undefined): { active: boolean; inactive: boolean } {
    if (status === 'inactive') return { active: false, inactive: true };
    if (status === 'all') return { active: true, inactive: true };
    return { active: true, inactive: false };
}

export function ProductFilterBar({ filters, onFilterChange, categories, taxTypes }: ProductFilterBarProps) {
    const { t } = useI18n();
    const [drawerOpen, setDrawerOpen] = useState(false);
    const [searchDraft, setSearchDraft] = useState(filters.searchTerm ?? '');

    useEffect(() => {
        setSearchDraft(filters.searchTerm ?? '');
    }, [filters.searchTerm]);

    const activeFilterCount = useMemo(() => countActiveProductFilters(filters), [filters]);

    const taxTypeOptions = useMemo((): ProductTaxTypeFilterOption[] => {
        if (taxTypes?.length) return taxTypes;
        return DEFAULT_TAX_TYPE_VALUES.map((value) => ({
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
    }, [taxTypes, t]);

    const stockOptions = useMemo(
        () =>
            STOCK_STATUS_OPTIONS.map((value) => ({
                value,
                label: t(`products.filters.stockStatus.${value}`),
            })),
        [t],
    );

    const categoryOptions = useMemo(
        () =>
            categories.map((c) => ({
                value: c.id ?? '',
                label: c.name?.trim() || c.id || '',
            })),
        [categories],
    );

    const patchFilters = useCallback(
        (patch: Partial<ProductFilters>) => {
            const next: ProductFilters = { ...filters, ...patch };
            (Object.keys(patch) as FilterKey[]).forEach((key) => {
                const value = patch[key];
                const emptyArray = Array.isArray(value) && value.length === 0;
                const emptyString = typeof value === 'string' && value.trim() === '';
                if (value == null || emptyArray || emptyString) {
                    delete next[key];
                }
            });
            onFilterChange(next);
        },
        [filters, onFilterChange],
    );

    const handleFilterChange = useCallback(
        (key: FilterKey, value: unknown) => {
            patchFilters({ [key]: value as ProductFilters[FilterKey] });
        },
        [patchFilters],
    );

    const handleSearch = useCallback(
        (value?: string) => {
            const term = (value ?? searchDraft).trim();
            if (!term) {
                patchFilters({
                    searchTerm: undefined,
                });
                return;
            }
            patchFilters({
                searchTerm: term,
                searchInName: true,
                searchInDescription: true,
                searchInBarcode: true,
            });
        },
        [patchFilters, searchDraft],
    );

    const clearAllFilters = useCallback(() => {
        setSearchDraft('');
        onFilterChange({
            status: 'active',
            searchInName: true,
            searchInDescription: true,
            searchInBarcode: true,
        });
    }, [onFilterChange]);

    const statusChecks = statusToCheckboxes(filters.status);

    const handleStatusCheckbox = useCallback(
        (kind: 'active' | 'inactive', checked: boolean) => {
            const nextActive = kind === 'active' ? checked : statusChecks.active;
            const nextInactive = kind === 'inactive' ? checked : statusChecks.inactive;
            patchFilters({ status: resolveStatusFromCheckboxes(nextActive, nextInactive) });
        },
        [patchFilters, statusChecks.active, statusChecks.inactive],
    );

    const priceSliderValue: [number, number] = [
        filters.minPrice ?? 0,
        filters.maxPrice ?? PRICE_SLIDER_MAX,
    ];

    const handlePriceSlider = useCallback(
        (range: number[]) => {
            const [min, max] = range;
            patchFilters({
                minPrice: min > 0 ? min : undefined,
                maxPrice: max < PRICE_SLIDER_MAX ? max : undefined,
            });
        },
        [patchFilters],
    );

    const taxLabelByValue = useMemo(
        () => new Map(taxTypeOptions.map((opt) => [opt.value, opt.label])),
        [taxTypeOptions],
    );

    const categoryNameById = useMemo(
        () => new Map(categoryOptions.map((opt) => [opt.value, opt.label])),
        [categoryOptions],
    );

    return (
        <>
            <Card size="small" style={{ marginBottom: 16, width: '100%' }}>
                <Space wrap style={{ width: '100%' }} align="center">
                    <Input.Search
                        placeholder={t('products.filters.searchPlaceholderFull')}
                        value={searchDraft}
                        onChange={(e) => setSearchDraft(e.target.value)}
                        onSearch={handleSearch}
                        enterButton={<SearchOutlined />}
                        allowClear
                        style={{ flex: 1, minWidth: 220, maxWidth: 560 }}
                    />
                    <Badge count={activeFilterCount} offset={[10, 0]}>
                        <Button icon={<FilterOutlined />} onClick={() => setDrawerOpen(true)}>
                            {t('products.filters.advanced')}
                        </Button>
                    </Badge>
                    {activeFilterCount > 0 ? (
                        <Button icon={<ClearOutlined />} onClick={clearAllFilters}>
                            {t('products.filters.clear')}
                        </Button>
                    ) : null}
                </Space>

                {activeFilterCount > 0 ? (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: '1px solid #f0f0f0' }}>
                        <Space wrap size={[8, 8]}>
                            {filters.searchTerm?.trim() ? (
                                <Tag closable onClose={() => handleFilterChange('searchTerm', undefined)}>
                                    {t('products.filters.chipSearch', { value: filters.searchTerm.trim() })}
                                </Tag>
                            ) : null}

                            {(filters.minPrice != null && Number.isFinite(filters.minPrice))
                                || (filters.maxPrice != null && Number.isFinite(filters.maxPrice)) ? (
                                <Tag
                                    closable
                                    onClose={() =>
                                        patchFilters({ minPrice: undefined, maxPrice: undefined })
                                    }
                                >
                                    {t('products.filters.chipPrice', {
                                        min: filters.minPrice ?? 0,
                                        max: filters.maxPrice ?? '∞',
                                    })}
                                </Tag>
                            ) : null}

                            {filters.stockStatus && filters.stockStatus !== 'All' ? (
                                <Tag closable onClose={() => handleFilterChange('stockStatus', undefined)}>
                                    {t('products.filters.chipStock', {
                                        value: t(`products.filters.stockStatus.${filters.stockStatus}`),
                                    })}
                                </Tag>
                            ) : null}

                            {filters.minStock != null && Number.isFinite(filters.minStock) ? (
                                <Tag closable onClose={() => handleFilterChange('minStock', undefined)}>
                                    {t('products.filters.chipMinStock', { value: filters.minStock })}
                                </Tag>
                            ) : null}

                            {filters.maxStock != null && Number.isFinite(filters.maxStock) ? (
                                <Tag closable onClose={() => handleFilterChange('maxStock', undefined)}>
                                    {t('products.filters.chipMaxStock', { value: filters.maxStock })}
                                </Tag>
                            ) : null}

                            {filters.categoryIds?.map((catId) => (
                                <Tag
                                    key={`cat-${catId}`}
                                    closable
                                    onClose={() => {
                                        const next = filters.categoryIds?.filter((id) => id !== catId);
                                        handleFilterChange('categoryIds', next?.length ? next : undefined);
                                    }}
                                >
                                    {t('products.filters.chipCategory', {
                                        value: categoryNameById.get(catId) ?? catId,
                                    })}
                                </Tag>
                            ))}

                            {filters.taxTypes?.map((tt) => (
                                <Tag
                                    key={`tax-${tt}`}
                                    closable
                                    onClose={() => {
                                        const next = filters.taxTypes?.filter((v) => v !== tt);
                                        handleFilterChange('taxTypes', next?.length ? next : undefined);
                                    }}
                                >
                                    {t('products.filters.chipTax', {
                                        value: taxLabelByValue.get(tt) ?? String(tt),
                                    })}
                                </Tag>
                            ))}

                            {filters.status === 'inactive' ? (
                                <Tag closable onClose={() => handleFilterChange('status', 'active')}>
                                    {t('products.filters.chipStatusInactive')}
                                </Tag>
                            ) : null}

                            {filters.status === 'all' ? (
                                <Tag closable onClose={() => handleFilterChange('status', 'active')}>
                                    {t('products.page.filterAll')}
                                </Tag>
                            ) : null}

                            {filters.isTaxable === true ? (
                                <Tag closable onClose={() => handleFilterChange('isTaxable', undefined)}>
                                    {t('products.filters.taxableYes')}
                                </Tag>
                            ) : null}

                            {filters.isTaxable === false ? (
                                <Tag closable onClose={() => handleFilterChange('isTaxable', undefined)}>
                                    {t('products.filters.taxableNo')}
                                </Tag>
                            ) : null}

                            {filters.createdRange?.[0] && filters.createdRange[1] ? (
                                <Tag closable onClose={() => handleFilterChange('createdRange', null)}>
                                    {t('products.filters.chipCreated', {
                                        from: filters.createdRange[0].format('DD.MM.YYYY'),
                                        to: filters.createdRange[1].format('DD.MM.YYYY'),
                                    })}
                                </Tag>
                            ) : null}
                        </Space>
                    </div>
                ) : null}
            </Card>

            <Drawer
                title={t('products.filters.drawerTitle')}
                placement="right"
                size={450}
                open={drawerOpen}
                onClose={() => setDrawerOpen(false)}
                extra={
                    activeFilterCount > 0 ? (
                        <Button type="link" icon={<ClearOutlined />} onClick={clearAllFilters}>
                            {t('products.filters.clearAll')}
                        </Button>
                    ) : null
                }
            >
                <Space orientation="vertical" size="large" style={{ width: '100%' }}>
                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{t('products.filters.search')}</div>
                        <Checkbox.Group
                            value={[
                                ...(filters.searchInName !== false ? ['name'] : []),
                                ...(filters.searchInDescription ? ['description'] : []),
                                ...(filters.searchInBarcode ? ['barcode'] : []),
                            ]}
                            onChange={(values) => {
                                patchFilters({
                                    searchInName: values.includes('name'),
                                    searchInDescription: values.includes('description'),
                                    searchInBarcode: values.includes('barcode'),
                                });
                            }}
                            options={[
                                { label: t('products.filters.searchInName'), value: 'name' },
                                { label: t('products.filters.searchInDescription'), value: 'description' },
                                { label: t('products.filters.searchInBarcode'), value: 'barcode' },
                            ]}
                        />
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 8 }}>
                            <DollarOutlined />
                            {t('products.filters.priceRangeSection')}
                        </div>
                        <Slider
                            range
                            min={0}
                            max={PRICE_SLIDER_MAX}
                            step={0.5}
                            value={priceSliderValue}
                            onChange={handlePriceSlider}
                            tooltip={{ formatter: (v) => `€${v}` }}
                        />
                        <Row gutter={16} style={{ marginTop: 12 }}>
                            <Col span={12}>
                                <InputNumber
                                    addonBefore="€"
                                    min={0}
                                    step={0.01}
                                    placeholder={t('products.filters.minPrice')}
                                    style={{ width: '100%' }}
                                    value={filters.minPrice}
                                    onChange={(v) => handleFilterChange('minPrice', v ?? undefined)}
                                />
                            </Col>
                            <Col span={12}>
                                <InputNumber
                                    addonBefore="€"
                                    min={0}
                                    step={0.01}
                                    placeholder={t('products.filters.maxPrice')}
                                    style={{ width: '100%' }}
                                    value={filters.maxPrice}
                                    onChange={(v) => handleFilterChange('maxPrice', v ?? undefined)}
                                />
                            </Col>
                        </Row>
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 8 }}>
                            <StockOutlined />
                            {t('products.filters.stockSection')}
                        </div>
                        <Select
                            allowClear
                            placeholder={t('products.filters.stockStatusPlaceholder')}
                            style={{ width: '100%' }}
                            options={stockOptions}
                            value={filters.stockStatus}
                            onChange={(v) => handleFilterChange('stockStatus', v)}
                        />
                    </div>

                    <Collapse
                        ghost
                        items={[
                            {
                                key: 'stock',
                                label: t('products.filters.customStockPanel'),
                                children: (
                                    <Row gutter={16}>
                                        <Col span={12}>
                                            <InputNumber
                                                min={0}
                                                placeholder={t('products.filters.minStock')}
                                                style={{ width: '100%' }}
                                                value={filters.minStock}
                                                onChange={(v) => handleFilterChange('minStock', v ?? undefined)}
                                            />
                                        </Col>
                                        <Col span={12}>
                                            <InputNumber
                                                min={0}
                                                placeholder={t('products.filters.maxStock')}
                                                style={{ width: '100%' }}
                                                value={filters.maxStock}
                                                onChange={(v) => handleFilterChange('maxStock', v ?? undefined)}
                                            />
                                        </Col>
                                    </Row>
                                ),
                            },
                        ]}
                    />

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500, display: 'flex', alignItems: 'center', gap: 8 }}>
                            <TagOutlined />
                            {t('products.filters.categories')}
                        </div>
                        <Select
                            mode="multiple"
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            placeholder={t('products.filters.categoriesPlaceholder')}
                            style={{ width: '100%' }}
                            options={categoryOptions}
                            value={filters.categoryIds}
                            onChange={(v) => handleFilterChange('categoryIds', v)}
                        />
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{t('products.filters.taxTypes')}</div>
                        <Checkbox.Group
                            value={filters.taxTypes}
                            onChange={(v) => handleFilterChange('taxTypes', v.length ? v : undefined)}
                            options={taxTypeOptions.map((opt) => ({ label: opt.label, value: opt.value }))}
                        />
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{t('products.filters.statusSection')}</div>
                        <Space>
                            <Checkbox
                                checked={statusChecks.active}
                                onChange={(e) => handleStatusCheckbox('active', e.target.checked)}
                            >
                                {t('products.page.filterActive')}
                            </Checkbox>
                            <Checkbox
                                checked={statusChecks.inactive}
                                onChange={(e) => handleStatusCheckbox('inactive', e.target.checked)}
                            >
                                {t('products.page.filterInactive')}
                            </Checkbox>
                        </Space>
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{t('products.filters.taxable')}</div>
                        <Select
                            allowClear
                            placeholder={t('products.filters.taxablePlaceholder')}
                            style={{ width: '100%' }}
                            options={[
                                { value: true, label: t('products.filters.taxableYes') },
                                { value: false, label: t('products.filters.taxableNo') },
                            ]}
                            value={filters.isTaxable}
                            onChange={(v) => handleFilterChange('isTaxable', v)}
                        />
                    </div>

                    <div>
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{t('products.filters.createdRange')}</div>
                        <RangePicker
                            style={{ width: '100%' }}
                            format="DD.MM.YYYY"
                            value={filters.createdRange as [Dayjs, Dayjs] | null | undefined}
                            onChange={(range) =>
                                handleFilterChange(
                                    'createdRange',
                                    range?.[0] && range[1] ? range : null,
                                )
                            }
                        />
                    </div>
                </Space>
            </Drawer>
        </>
    );
}
