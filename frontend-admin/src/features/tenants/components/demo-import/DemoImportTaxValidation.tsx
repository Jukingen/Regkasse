'use client';

import { Alert, Button, List, Space, Typography } from 'antd';
import { WarningOutlined } from '@ant-design/icons';

import type { DemoImportCatalog } from '@/api/admin/products';
import type { CategoryGroup } from '@/features/tenants/components/demo-import/categoryGroups';
import {
    TAX_BULK_FIX_PRESETS,
    analyzeTaxSelection,
    applyCategoryDefaultTax,
    applyTaxRateToProducts,
    buildCategoryVatLookup,
    collectProductIdsForBulkPreset,
    type TaxValidationSummary,
} from '@/features/tenants/components/demo-import/taxValidation';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';
import { formatTaxRateLabel } from '@/features/tenants/components/demo-import/utils';

const { Text } = Typography;

export type DemoImportTaxValidationProps = {
    catalog: DemoImportCatalog | undefined;
    categoryGroups: CategoryGroup[];
    catalogProducts: CatalogProduct[];
    selectedProducts: CatalogProduct[];
    selectedProductIds: Set<string>;
    taxOverrides: Record<string, number>;
    onTaxOverridesChange: (next: Record<string, number>) => void;
};

export function useTaxValidationSummary(
    catalog: DemoImportCatalog | undefined,
    selectedProducts: CatalogProduct[],
    taxOverrides: Record<string, number>,
): TaxValidationSummary {
    const categoryVatByName = buildCategoryVatLookup(catalog);
    return analyzeTaxSelection(selectedProducts, categoryVatByName, taxOverrides);
}

export function DemoImportTaxValidation({
    catalog,
    categoryGroups,
    catalogProducts,
    selectedProducts,
    selectedProductIds,
    taxOverrides,
    onTaxOverridesChange,
}: DemoImportTaxValidationProps) {
    const categoryVatByName = buildCategoryVatLookup(catalog);
    const summary = analyzeTaxSelection(selectedProducts, categoryVatByName, taxOverrides);

    const errorIssues = summary.issues.filter((i) => i.severity === 'error');
    const warningIssues = summary.issues.filter((i) => i.severity === 'warning');

    const applyBulkPreset = (presetId: string) => {
        const preset = TAX_BULK_FIX_PRESETS.find((p) => p.id === presetId);
        if (!preset) return;
        const ids = collectProductIdsForBulkPreset(
            preset,
            categoryGroups,
            catalogProducts,
            selectedProductIds,
        );
        if (ids.length === 0) return;
        onTaxOverridesChange(applyTaxRateToProducts(ids, preset.rate, taxOverrides));
    };

    const fixCategoryDefaults = (categoryName: string) => {
        onTaxOverridesChange(
            applyCategoryDefaultTax(
                categoryName,
                catalogProducts,
                selectedProductIds,
                categoryVatByName,
                taxOverrides,
            ),
        );
    };

    const hasIssues = summary.issues.length > 0;

    return (
        <Space orientation="vertical" style={{ width: '100%' }} size="small">
            <Text type="secondary">Schnellkorrektur:</Text>
            <Space wrap>
                {TAX_BULK_FIX_PRESETS.map((preset) => (
                    <Button key={preset.id} size="small" onClick={() => applyBulkPreset(preset.id)}>
                        {preset.label}
                    </Button>
                ))}
            </Space>

            {!hasIssues ? (
                <Alert type="success" showIcon title="Keine Steuerprobleme in der Auswahl" />
            ) : null}

            {errorIssues.length > 0 ? (
                <Alert
                    type="error"
                    showIcon
                    title={`${errorIssues.length} Produkt(e) mit ungültigem Steuersatz`}
                    description={
                        <List
                            size="small"
                            dataSource={errorIssues.slice(0, 8)}
                            renderItem={(item) => <List.Item style={{ padding: '4px 0' }}>{item.message}</List.Item>}
                        />
                    }
                />
            ) : null}

            {warningIssues.length > 0 ? (
                <Alert
                    type="warning"
                    showIcon
                    icon={<WarningOutlined />}
                    title={`${warningIssues.length} Hinweis(e) zur Steuerprüfung`}
                    description={
                        <List
                            size="small"
                            dataSource={warningIssues.slice(0, 12)}
                            renderItem={(item) => (
                                <List.Item
                                    style={{ padding: '4px 0', display: 'flex', justifyContent: 'space-between' }}
                                >
                                    <span>{item.message}</span>
                                    {item.type === 'category_mismatch' && item.categoryName ? (
                                        <Button
                                            type="link"
                                            size="small"
                                            onClick={() => fixCategoryDefaults(item.categoryName!)}
                                        >
                                            Standard ({formatTaxRateLabel(item.expectedRate ?? 10)})
                                        </Button>
                                    ) : null}
                                    {item.type === 'mixed_category_rates' && item.categoryName ? (
                                        <Button
                                            type="link"
                                            size="small"
                                            onClick={() => fixCategoryDefaults(item.categoryName!)}
                                        >
                                            Vereinheitlichen
                                        </Button>
                                    ) : null}
                                </List.Item>
                            )}
                        />
                    }
                />
            ) : null}
        </Space>
    );
}
