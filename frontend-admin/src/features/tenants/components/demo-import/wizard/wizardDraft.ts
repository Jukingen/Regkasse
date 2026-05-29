import type { DemoImportProductOverride } from '@/api/admin/products';
import type { DemoImportPriceAdjustmentState } from '@/features/tenants/components/demo-import/priceAdjustment';
import { applyPriceAdjustment } from '@/features/tenants/components/demo-import/priceAdjustment';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';
import { PREDEFINED_TAX_RATES } from '@/features/tenants/components/demo-import/utils';

export const WIZARD_STEP_COUNT = 6;

export const VALID_TAX_RATES = PREDEFINED_TAX_RATES.map((t) => t.rate);

export type WizardDraft = {
    selectedGroupNames: Set<string>;
    selectedProductIds: Set<string>;
    priceOverrides: Record<string, number>;
    taxOverrides: Record<string, number>;
};

export function resolveWizardPrice(
    product: CatalogProduct,
    priceAdjustment: DemoImportPriceAdjustmentState,
    priceOverrides: Record<string, number>,
): number {
    if (priceOverrides[product.id] !== undefined) {
        return priceOverrides[product.id];
    }
    return applyPriceAdjustment(Number(product.price), priceAdjustment);
}

export function resolveWizardTax(product: CatalogProduct, taxOverrides: Record<string, number>): number {
    if (taxOverrides[product.id] !== undefined) {
        return taxOverrides[product.id];
    }
    return Number(product.taxRate);
}

export function isValidTaxRate(rate: number): boolean {
    return VALID_TAX_RATES.includes(rate as (typeof VALID_TAX_RATES)[number]);
}

export function buildProductOverridesForApi(
    products: CatalogProduct[],
    selectedIds: Set<string>,
    priceAdjustment: DemoImportPriceAdjustmentState,
    priceOverrides: Record<string, number>,
    taxOverrides: Record<string, number>,
): DemoImportProductOverride[] {
    const overrides: DemoImportProductOverride[] = [];

    for (const product of products) {
        if (!selectedIds.has(product.id)) continue;

        const entry: DemoImportProductOverride = { catalogProductId: product.id };
        let hasOverride = false;

        if (priceOverrides[product.id] !== undefined) {
            entry.price = priceOverrides[product.id];
            hasOverride = true;
        }

        if (taxOverrides[product.id] !== undefined) {
            entry.taxRate = taxOverrides[product.id];
            hasOverride = true;
        }

        if (hasOverride) {
            overrides.push(entry);
        }
    }

    return overrides;
}

export function countSelectedGroupsWithProducts(
    selectedGroupNames: Set<string>,
    getGroupProductIds: (groupName: string) => string[],
): number {
    let count = 0;
    for (const name of selectedGroupNames) {
        if (getGroupProductIds(name).length > 0) count++;
    }
    return count;
}
