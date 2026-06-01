'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useCallback, useMemo, useState } from 'react';
import { Alert, Button, Checkbox, Collapse, InputNumber, Select, Space, Spin, Steps, Table, Tag, Typography } from 'antd';
import { CheckSquareOutlined } from '@ant-design/icons';

import type { DemoImportCatalog } from '@/api/admin/products';
import { DemoImportProfileBar } from '@/features/tenants/components/demo-import/DemoImportProfileBar';
import type { AppliedDemoImportProfile } from '@/features/tenants/components/demo-import/importProfiles';
import { DemoImportBulkMenu } from '@/features/tenants/components/demo-import/DemoImportBulkMenu';
import { DemoImportCategoryPanel } from '@/features/tenants/components/demo-import/DemoImportCategoryPanel';
import { DemoImportImageSection } from '@/features/tenants/components/demo-import/DemoImportImageSection';
import { DemoImportPriceAdjustmentSection } from '@/features/tenants/components/demo-import/DemoImportPriceAdjustment';
import type { DemoImportImageMode } from '@/features/tenants/components/demo-import/demoImportImage';
import {
    DEFAULT_PRICE_ADJUSTMENT,
    type DemoImportPriceAdjustmentState,
} from '@/features/tenants/components/demo-import/priceAdjustment';
import {
    CATEGORY_GROUPS,
    enrichGroupsWithCatalog,
    type CategoryGroup,
} from '@/features/tenants/components/demo-import/categoryGroups';
import {
    buildProductOverridesForApi,
    isValidTaxRate,
    resolveWizardPrice,
    resolveWizardTax,
    WIZARD_STEP_COUNT,
} from '@/features/tenants/components/demo-import/wizard/wizardDraft';
import { WIZARD_STEPS } from '@/features/tenants/components/demo-import/wizard/wizardSteps';
import {
    DemoImportTaxValidation,
    useTaxValidationSummary,
} from '@/features/tenants/components/demo-import/DemoImportTaxValidation';
import {
    formatDemoProductName,
    formatEuro,
    formatTaxRateLabel,
    getGroupProductIds,
    getProductsForGroup,
    isGroupFullySelected,
    isGroupPartiallySelected,
    type CatalogProduct,
    toggleProductIds,
} from '@/features/tenants/components/demo-import/utils';

const { Text, Paragraph } = Typography;
const { Panel } = Collapse;

export type DemoImportCatalogWizardProps = {
    catalog: DemoImportCatalog | undefined;
    isLoading: boolean;
    isError: boolean;
    tenantId?: string;
    priceAdjustment: DemoImportPriceAdjustmentState;
    onPriceAdjustmentChange: (v: DemoImportPriceAdjustmentState) => void;
    imageMode: DemoImportImageMode;
    onImageModeChange: (v: DemoImportImageMode) => void;
    overwrite: boolean;
    onOverwriteChange: (v: boolean) => void;
    onRequestImport: (draft: {
        selectedProductIds: Set<string>;
        productOverrides: ReturnType<typeof buildProductOverridesForApi>;
    }) => void;
    importLoading: boolean;
};

export function DemoImportCatalogWizard({
    catalog,
    isLoading,
    isError,
    tenantId,
    priceAdjustment,
    onPriceAdjustmentChange,
    imageMode,
    onImageModeChange,
    overwrite,
    onOverwriteChange,
    onRequestImport,
    importLoading,
}: DemoImportCatalogWizardProps) {
  const { message } = useAntdApp();

    const [currentStep, setCurrentStep] = useState(0);
    const [selectedGroupNames, setSelectedGroupNames] = useState<Set<string>>(new Set());
    const [selectedProductIds, setSelectedProductIds] = useState<Set<string>>(new Set());
    const [expandedGroups, setExpandedGroups] = useState<string[]>(['pizzas', 'salads']);
    const [priceOverrides, setPriceOverrides] = useState<Record<string, number>>({});
    const [taxOverrides, setTaxOverrides] = useState<Record<string, number>>({});

    const catalogProducts = catalog?.products ?? [];

    const categoryGroups = useMemo(() => {
        const countByName = new Map(
            catalog?.categories.map((c) => [c.name, c.productCount]) ?? [],
        );
        return enrichGroupsWithCatalog(CATEGORY_GROUPS, countByName);
    }, [catalog]);

    const activeGroups = useMemo(
        () => categoryGroups.filter((g) => selectedGroupNames.has(g.name)),
        [categoryGroups, selectedGroupNames],
    );

    const selectedProducts = useMemo(
        () => catalogProducts.filter((p) => selectedProductIds.has(p.id)),
        [catalogProducts, selectedProductIds],
    );

    const resetWizard = useCallback(() => {
        setCurrentStep(0);
        setSelectedGroupNames(new Set());
        setSelectedProductIds(new Set());
        setExpandedGroups(['pizzas', 'salads']);
        setPriceOverrides({});
        setTaxOverrides({});
    }, []);

    const handleGroupToggle = (group: CategoryGroup, checked: boolean) => {
        setSelectedGroupNames((prev) => {
            const next = new Set(prev);
            if (checked) next.add(group.name);
            else next.delete(group.name);
            return next;
        });
        const ids = getGroupProductIds(group, catalogProducts);
        setSelectedProductIds((prev) => toggleProductIds(prev, ids, checked));
    };

    const handleToggleProduct = useCallback((productId: string, selected: boolean) => {
        setSelectedProductIds((prev) => toggleProductIds(prev, [productId], selected));
    }, []);

    const handleSetProductIds = useCallback((productIds: string[], selected: boolean) => {
        setSelectedProductIds((prev) => toggleProductIds(prev, productIds, selected));
    }, []);

    const handleApplyProfile = useCallback(
        (applied: AppliedDemoImportProfile) => {
            setSelectedGroupNames(applied.groupNames);
            setSelectedProductIds(applied.productIds);
            setExpandedGroups(applied.expandedGroups);
            setPriceOverrides({});
            setTaxOverrides({});
            if (applied.priceAdjustment) onPriceAdjustmentChange(applied.priceAdjustment);
            if (applied.imageMode) onImageModeChange(applied.imageMode);
            if (applied.overwrite !== undefined) onOverwriteChange(applied.overwrite);
        },
        [onImageModeChange, onOverwriteChange, onPriceAdjustmentChange],
    );

    const setPriceOverride = (productId: string, price: number | null) => {
        setPriceOverrides((prev) => {
            const next = { ...prev };
            if (price === null || !Number.isFinite(price)) delete next[productId];
            else next[productId] = price;
            return next;
        });
    };

    const setTaxOverride = (productId: string, taxRate: number) => {
        setTaxOverrides((prev) => ({ ...prev, [productId]: taxRate }));
    };

    const applyTaxToAllSelected = (rate: number) => {
        setTaxOverrides((prev) => {
            const next = { ...prev };
            for (const p of selectedProducts) next[p.id] = rate;
            return next;
        });
    };

    const canGoNext = (): boolean => {
        switch (currentStep) {
            case 0:
                return selectedGroupNames.size > 0;
            case 1:
                return selectedProductIds.size > 0;
            case 3:
                return selectedProducts.every((p) =>
                    isValidTaxRate(resolveWizardTax(p, taxOverrides)),
                );
            default:
                return true;
        }
    };

    const goNext = () => {
        if (!canGoNext()) {
            if (currentStep === 0) message.warning('Bitte mindestens eine Kategorie wählen.');
            else if (currentStep === 1) message.warning('Bitte mindestens ein Produkt wählen.');
            else if (currentStep === 3) message.warning('Ungültige Steuersätze — bitte korrigieren.');
            return;
        }
        if (currentStep === 0 && selectedProductIds.size === 0) {
            const ids: string[] = [];
            for (const group of categoryGroups) {
                if (selectedGroupNames.has(group.name)) {
                    ids.push(...getGroupProductIds(group, catalogProducts));
                }
            }
            setSelectedProductIds(new Set(ids));
        }
        setCurrentStep((s) => Math.min(s + 1, WIZARD_STEP_COUNT - 1));
    };

    const goBack = () => setCurrentStep((s) => Math.max(s - 1, 0));

    const totalValue = selectedProducts.reduce(
        (sum, p) => sum + resolveWizardPrice(p, priceAdjustment, priceOverrides),
        0,
    );

    const taxValidation = useTaxValidationSummary(catalog, selectedProducts, taxOverrides);
    const invalidTaxCount = taxValidation.invalidProductIds.size;

    if (isLoading) {
        return (
            <div style={{ textAlign: 'center', padding: 32 }}>
                <Spin />
            </div>
        );
    }

    if (isError) {
        return <Alert type="error" showIcon title="Katalog konnte nicht geladen werden" />;
    }

    const previewColumns = [
        {
            title: 'Produkt',
            dataIndex: 'name',
            key: 'name',
            ellipsis: true,
            render: (_: string, row: CatalogProduct) => formatDemoProductName(row.name),
        },
        { title: 'Kategorie', dataIndex: 'category', key: 'category', width: 120, ellipsis: true },
        {
            title: 'Preis',
            key: 'price',
            width: 88,
            align: 'right' as const,
            render: (_: unknown, row: CatalogProduct) =>
                formatEuro(resolveWizardPrice(row, priceAdjustment, priceOverrides)),
        },
        {
            title: 'MwSt',
            key: 'tax',
            width: 100,
            render: (_: unknown, row: CatalogProduct) => {
                const rate = resolveWizardTax(row, taxOverrides);
                const ok = isValidTaxRate(rate);
                return <Tag color={ok ? 'blue' : 'error'}>{formatTaxRateLabel(rate)}</Tag>;
            },
        },
    ];

    const priceEditColumns = [
        ...previewColumns.slice(0, 2),
        {
            title: 'Katalog',
            key: 'catalogPrice',
            width: 80,
            align: 'right' as const,
            render: (_: unknown, row: CatalogProduct) => formatEuro(Number(row.price)),
        },
        {
            title: 'Import-Preis',
            key: 'importPrice',
            width: 110,
            render: (_: unknown, row: CatalogProduct) => (
                <InputNumber
                    size="small"
                    min={0}
                    step={0.1}
                    value={resolveWizardPrice(row, priceAdjustment, priceOverrides)}
                    onChange={(v) => setPriceOverride(row.id, typeof v === 'number' ? v : null)}
                    style={{ width: '100%' }}
                />
            ),
        },
    ];

    const taxEditColumns = [
        ...previewColumns.slice(0, 2),
        {
            title: 'Steuersatz',
            key: 'taxEdit',
            width: 160,
            render: (_: unknown, row: CatalogProduct) => {
                const rate = resolveWizardTax(row, taxOverrides);
                const hasError = taxValidation.invalidProductIds.has(row.id);
                const hasWarning =
                    taxValidation.mismatchProductIds.has(row.id) ||
                    taxValidation.mixedCategoryNames.has(row.category);
                return (
                    <Select
                        size="small"
                        status={hasError ? 'error' : hasWarning ? 'warning' : undefined}
                        value={rate}
                        onChange={(v) => setTaxOverride(row.id, v)}
                        options={[
                            { value: 20, label: '20% (Standard)' },
                            { value: 10, label: '10% (ermäßigt)' },
                            { value: 13, label: '13% (Sondersatz)' },
                            { value: 0, label: '0%' },
                        ]}
                        style={{ width: '100%' }}
                    />
                );
            },
        },
    ];

    return (
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Steps
                current={currentStep}
                size="small"
                items={WIZARD_STEPS.map((s) => ({ title: s.title }))}
                style={{ marginBottom: 8 }}
            />

            <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {WIZARD_STEPS[currentStep]?.description}
            </Paragraph>

            {currentStep === 0 ? (
                <div style={{ maxHeight: 420, overflow: 'auto' }}>
                    <DemoImportProfileBar
                        tenantId={tenantId}
                        categoryGroups={categoryGroups}
                        catalogProducts={catalogProducts}
                        selectedGroupNames={selectedGroupNames}
                        selectedProductIds={selectedProductIds}
                        priceAdjustment={priceAdjustment}
                        imageMode={imageMode}
                        overwrite={overwrite}
                        onApply={handleApplyProfile}
                    />
                    <Space style={{ marginBottom: 12 }}>
                        <Button
                            size="small"
                            icon={<CheckSquareOutlined />}
                            onClick={() => {
                                const all = new Set(categoryGroups.map((g) => g.name));
                                setSelectedGroupNames(all);
                                setSelectedProductIds(new Set(catalogProducts.map((p) => p.id)));
                            }}
                        >
                            Alle Kategorien
                        </Button>
                        <Button
                            size="small"
                            onClick={() => {
                                setSelectedGroupNames(new Set());
                                setSelectedProductIds(new Set());
                            }}
                        >
                            Alle abwählen
                        </Button>
                    </Space>
                    {categoryGroups.map((group) => {
                        const checked = selectedGroupNames.has(group.name);
                        const full = isGroupFullySelected(group, catalogProducts, selectedProductIds);
                        const partial = isGroupPartiallySelected(group, catalogProducts, selectedProductIds);
                        return (
                            <div
                                key={group.name}
                                style={{
                                    padding: '10px 12px',
                                    borderBottom: '1px solid #f0f0f0',
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 12,
                                }}
                            >
                                <Checkbox
                                    checked={checked && full}
                                    indeterminate={checked && partial && !full}
                                    onChange={(e) => handleGroupToggle(group, e.target.checked)}
                                />
                                <span style={{ fontSize: 22 }}>{group.icon}</span>
                                <div style={{ flex: 1 }}>
                                    <Text strong>{group.displayName}</Text>
                                    <br />
                                    <Text type="secondary" style={{ fontSize: 12 }}>
                                        {group.productCount} Produkte
                                    </Text>
                                </div>
                            </div>
                        );
                    })}
                </div>
            ) : null}

            {currentStep === 1 ? (
                <div style={{ maxHeight: 420, overflow: 'auto' }}>
                    <Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
                        {selectedProductIds.size} von {catalogProducts.length} Produkten ausgewählt
                    </Text>
                    <Collapse
                        activeKey={expandedGroups}
                        onChange={(keys) => setExpandedGroups(keys as string[])}
                        ghost
                    >
                        {activeGroups.map((group) => {
                            const groupProducts = getProductsForGroup(group, catalogProducts);
                            const groupChecked = isGroupFullySelected(group, catalogProducts, selectedProductIds);
                            const groupIndeterminate = isGroupPartiallySelected(
                                group,
                                catalogProducts,
                                selectedProductIds,
                            );
                            return (
                                <Panel
                                    key={group.name}
                                    header={
                                        <div
                                            style={{
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                width: '100%',
                                            }}
                                        >
                                            <Checkbox
                                                checked={groupChecked}
                                                indeterminate={groupIndeterminate}
                                                onChange={(e) => handleGroupToggle(group, e.target.checked)}
                                                onClick={(e) => e.stopPropagation()}
                                            >
                                                <Space>
                                                    <span>{group.icon}</span>
                                                    <Text strong>{group.displayName}</Text>
                                                </Space>
                                            </Checkbox>
                                            <DemoImportBulkMenu
                                                products={groupProducts}
                                                onSetProductIds={handleSetProductIds}
                                            />
                                        </div>
                                    }
                                >
                                    <DemoImportCategoryPanel
                                        group={group}
                                        products={groupProducts}
                                        selectedProductIds={selectedProductIds}
                                        imageMode={imageMode}
                                        onToggleProduct={handleToggleProduct}
                                        onSetProductIds={handleSetProductIds}
                                    />
                                </Panel>
                            );
                        })}
                    </Collapse>
                </div>
            ) : null}

            {currentStep === 2 ? (
                <Space orientation="vertical" style={{ width: '100%' }}>
                    <DemoImportPriceAdjustmentSection
                        value={priceAdjustment}
                        onChange={onPriceAdjustmentChange}
                        selectedProductCount={selectedProductIds.size}
                    />
                    <Table
                        size="small"
                        rowKey="id"
                        dataSource={selectedProducts.slice(0, 50)}
                        columns={priceEditColumns}
                        pagination={
                            selectedProducts.length > 50
                                ? { pageSize: 50, showSizeChanger: false }
                                : false
                        }
                        scroll={{ y: 240 }}
                    />
                    {selectedProducts.length > 50 ? (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            Zeigt die ersten 50 Produkte — Massenänderung über „Preis Anpassung“ oben.
                        </Text>
                    ) : null}
                </Space>
            ) : null}

            {currentStep === 3 ? (
                <Space orientation="vertical" style={{ width: '100%' }}>
                    <DemoImportTaxValidation
                        catalog={catalog}
                        categoryGroups={categoryGroups}
                        catalogProducts={catalogProducts}
                        selectedProducts={selectedProducts}
                        selectedProductIds={selectedProductIds}
                        taxOverrides={taxOverrides}
                        onTaxOverridesChange={setTaxOverrides}
                    />
                    <Space wrap>
                        <Text type="secondary">Alle ausgewählten:</Text>
                        <Button size="small" onClick={() => applyTaxToAllSelected(10)}>
                            10%
                        </Button>
                        <Button size="small" onClick={() => applyTaxToAllSelected(13)}>
                            13%
                        </Button>
                        <Button size="small" onClick={() => applyTaxToAllSelected(20)}>
                            20%
                        </Button>
                    </Space>
                    <Table
                        size="small"
                        rowKey="id"
                        dataSource={selectedProducts}
                        columns={taxEditColumns}
                        onRow={(row) => {
                            if (taxValidation.invalidProductIds.has(row.id)) {
                                return { style: { background: '#fff2f0' } };
                            }
                            if (
                                taxValidation.mismatchProductIds.has(row.id) ||
                                taxValidation.mixedCategoryNames.has(row.category)
                            ) {
                                return { style: { background: '#fffbe6' } };
                            }
                            return {};
                        }}
                        pagination={{ pageSize: 15, showSizeChanger: true, size: 'small' }}
                        scroll={{ y: 240 }}
                    />
                </Space>
            ) : null}

            {currentStep === 4 ? (
                <>
                    <Space wrap style={{ marginBottom: 12 }}>
                        <Tag color="blue">{selectedGroupNames.size} Kategorien</Tag>
                        <Tag color="green">{selectedProductIds.size} Produkte</Tag>
                        <Tag>Gesamtwert {formatEuro(totalValue)}</Tag>
                    </Space>
                    <Table
                        size="small"
                        rowKey="id"
                        dataSource={selectedProducts}
                        columns={previewColumns}
                        pagination={{ pageSize: 20, showSizeChanger: true, size: 'small' }}
                        scroll={{ y: 320 }}
                    />
                </>
            ) : null}

            {currentStep === 5 ? (
                <Space orientation="vertical" style={{ width: '100%' }} size="middle">
                    <Alert
                        type="info"
                        showIcon
                        title="Bereit zum Import"
                        description={
                            <>
                                Es werden <strong>{selectedProductIds.size}</strong> Produkte in{' '}
                                <strong>{selectedGroupNames.size}</strong> Kategoriegruppen importiert
                                (Gesamtwert {formatEuro(totalValue)}).
                            </>
                        }
                    />
                    <DemoImportImageSection value={imageMode} onChange={onImageModeChange} />
                    <div style={{ background: '#fffbe6', padding: 12, borderRadius: 6 }}>
                        <Checkbox checked={overwrite} onChange={(e) => onOverwriteChange(e.target.checked)}>
                            Vorhandene Produkte überschreiben (gleicher Name)
                        </Checkbox>
                    </div>
                    <Button type="link" size="small" onClick={resetWizard} style={{ padding: 0 }}>
                        Auswahl zurücksetzen
                    </Button>
                </Space>
            ) : null}

            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    marginTop: 8,
                    paddingTop: 12,
                    borderTop: '1px solid #f0f0f0',
                }}
            >
                <Button disabled={currentStep === 0} onClick={goBack}>
                    Zurück
                </Button>
                <Space>
                    {currentStep < WIZARD_STEP_COUNT - 1 ? (
                        <Button type="primary" onClick={goNext} disabled={!canGoNext()}>
                            Weiter
                        </Button>
                    ) : (
                        <Button
                            type="primary"
                            loading={importLoading}
                            disabled={selectedProductIds.size === 0}
                            onClick={() =>
                                onRequestImport({
                                    selectedProductIds,
                                    productOverrides: buildProductOverridesForApi(
                                        catalogProducts,
                                        selectedProductIds,
                                        priceAdjustment,
                                        priceOverrides,
                                        taxOverrides,
                                    ),
                                })
                            }
                        >
                            {selectedProductIds.size} Produkte importieren ({formatEuro(totalValue)})
                        </Button>
                    )}
                </Space>
            </div>
        </Space>
    );
}
