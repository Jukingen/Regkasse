import type { DemoImportCatalog } from '@/api/admin/products';
import type { CategoryGroup } from '@/features/tenants/components/demo-import/categoryGroups';
import { getProductsForGroup } from '@/features/tenants/components/demo-import/utils';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';
import {
    isValidTaxRate,
    resolveWizardTax,
} from '@/features/tenants/components/demo-import/wizard/wizardDraft';
import { formatTaxRateLabel } from '@/features/tenants/components/demo-import/utils';

export type TaxIssueType = 'invalid_rate' | 'category_mismatch' | 'mixed_category_rates';

export type TaxIssue = {
    type: TaxIssueType;
    severity: 'error' | 'warning';
    message: string;
    productId?: string;
    categoryName?: string;
    expectedRate?: number;
    actualRate?: number;
    rates?: number[];
};

export type TaxValidationSummary = {
    issues: TaxIssue[];
    invalidProductIds: Set<string>;
    mismatchProductIds: Set<string>;
    mixedCategoryNames: Set<string>;
};

export type TaxBulkFixPreset = {
    id: string;
    label: string;
    rate: number;
    matchGroup: (group: CategoryGroup) => boolean;
};

/** Food-related wizard groups (Speisen) vs drinks. */
export const TAX_BULK_FIX_PRESETS: TaxBulkFixPreset[] = [
    {
        id: 'drinks-20',
        label: 'Alle Getränke auf 20% Steuer setzen',
        rate: 20,
        matchGroup: (g) => g.name === 'drinks',
    },
    {
        id: 'food-10',
        label: 'Alle Speisen auf 10% Steuer setzen',
        rate: 10,
        matchGroup: (g) =>
            g.name !== 'drinks' &&
            ['pizzas', 'salads', 'pasta', 'burgers', 'kebap', 'snacks', 'desserts'].includes(g.name),
    },
];

export function buildCategoryVatLookup(catalog: DemoImportCatalog | undefined): Map<string, number> {
    const map = new Map<string, number>();
    if (!catalog) return map;
    for (const cat of catalog.categories) {
        map.set(cat.name, Number(cat.vatRate));
    }
    return map;
}

export function analyzeTaxSelection(
    selectedProducts: CatalogProduct[],
    categoryVatByName: Map<string, number>,
    taxOverrides: Record<string, number>,
): TaxValidationSummary {
    const issues: TaxIssue[] = [];
    const invalidProductIds = new Set<string>();
    const mismatchProductIds = new Set<string>();
    const mixedCategoryNames = new Set<string>();

    for (const product of selectedProducts) {
        const actual = resolveWizardTax(product, taxOverrides);
        const expected = categoryVatByName.get(product.category) ?? 10;

        if (!isValidTaxRate(actual)) {
            invalidProductIds.add(product.id);
            issues.push({
                type: 'invalid_rate',
                severity: 'error',
                productId: product.id,
                categoryName: product.category,
                actualRate: actual,
                message: `„${product.name}": ungültiger Steuersatz ${actual}% (erlaubt: 0, 10, 13, 20)`,
            });
            continue;
        }

        if (actual !== expected) {
            mismatchProductIds.add(product.id);
            issues.push({
                type: 'category_mismatch',
                severity: 'warning',
                productId: product.id,
                categoryName: product.category,
                expectedRate: expected,
                actualRate: actual,
                message: `„${product.name}": ${formatTaxRateLabel(actual)} — Kategorie-Standard ist ${formatTaxRateLabel(expected)}`,
            });
        }
    }

    const byCategory = new Map<string, CatalogProduct[]>();
    for (const product of selectedProducts) {
        const list = byCategory.get(product.category) ?? [];
        list.push(product);
        byCategory.set(product.category, list);
    }

    for (const [categoryName, products] of byCategory) {
        const rates = new Set(products.map((p) => resolveWizardTax(p, taxOverrides)));
        if (rates.size <= 1) continue;

        mixedCategoryNames.add(categoryName);
        const rateList = [...rates].sort((a, b) => b - a);
        issues.push({
            type: 'mixed_category_rates',
            severity: 'warning',
            categoryName,
            rates: rateList,
            message: `Kategorie „${categoryName}": gemischte Steuersätze (${rateList.map((r) => formatTaxRateLabel(r)).join(', ')})`,
        });
    }

    return { issues, invalidProductIds, mismatchProductIds, mixedCategoryNames };
}

export function applyTaxRateToProducts(
    productIds: string[],
    rate: number,
    taxOverrides: Record<string, number>,
): Record<string, number> {
    const next = { ...taxOverrides };
    for (const id of productIds) {
        next[id] = rate;
    }
    return next;
}

export function collectProductIdsForBulkPreset(
    preset: TaxBulkFixPreset,
    categoryGroups: CategoryGroup[],
    catalogProducts: CatalogProduct[],
    selectedProductIds: Set<string>,
): string[] {
    const ids: string[] = [];
    for (const group of categoryGroups) {
        if (!preset.matchGroup(group)) continue;
        for (const product of getProductsForGroup(group, catalogProducts)) {
            if (selectedProductIds.has(product.id)) ids.push(product.id);
        }
    }
    return ids;
}

export function applyCategoryDefaultTax(
    categoryName: string,
    products: CatalogProduct[],
    selectedProductIds: Set<string>,
    categoryVatByName: Map<string, number>,
    taxOverrides: Record<string, number>,
): Record<string, number> {
    const rate = categoryVatByName.get(categoryName) ?? 10;
    const ids = products
        .filter((p) => p.category === categoryName && selectedProductIds.has(p.id))
        .map((p) => p.id);
    return applyTaxRateToProducts(ids, rate, taxOverrides);
}
