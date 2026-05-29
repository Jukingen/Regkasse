import type { DemoImportCatalog, DemoImportRequest } from '@/api/admin/products';
import type { CategoryGroup } from './categoryGroups';

export type CatalogProduct = DemoImportCatalog['products'][number];

/** Predefined Austrian VAT rates used in demo catalog bulk filters. */
export const PREDEFINED_TAX_RATES = [
    { rate: 20, label: '20% (Standard)' },
    { rate: 10, label: '10% (ermäßigt)' },
    { rate: 13, label: '13% (Sondersatz)' },
    { rate: 0, label: '0%' },
] as const;

export const PRODUCTS_PAGE_SIZE = 8;

export function getGroupCategoryNames(group: CategoryGroup): string[] {
    return group.subcategories?.map((sub) => sub.name) ?? [group.name];
}

export function getProductsForGroup(group: CategoryGroup, catalogProducts: CatalogProduct[]): CatalogProduct[] {
    const names = new Set(getGroupCategoryNames(group));
    return catalogProducts.filter((p) => names.has(p.category));
}

export function getGroupProductIds(group: CategoryGroup, catalogProducts: CatalogProduct[]): string[] {
    return getProductsForGroup(group, catalogProducts).map((p) => p.id);
}

export function isGroupFullySelected(
    group: CategoryGroup,
    catalogProducts: CatalogProduct[],
    selectedIds: Set<string>,
): boolean {
    const ids = getGroupProductIds(group, catalogProducts);
    return ids.length > 0 && ids.every((id) => selectedIds.has(id));
}

export function isGroupPartiallySelected(
    group: CategoryGroup,
    catalogProducts: CatalogProduct[],
    selectedIds: Set<string>,
): boolean {
    const ids = getGroupProductIds(group, catalogProducts);
    const selectedCount = ids.filter((id) => selectedIds.has(id)).length;
    return selectedCount > 0 && selectedCount < ids.length;
}

export function formatEuro(amount: number): string {
    return `€${amount.toFixed(2).replace('.', ',')}`;
}

export function formatDemoProductName(name: string): string {
    return name
        .split(' ')
        .filter(Boolean)
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');
}

export function formatTaxRateLabel(taxRate: number): string {
    const predefined = PREDEFINED_TAX_RATES.find((t) => t.rate === taxRate);
    if (predefined) return predefined.label;
    return `${taxRate}%`;
}

export function taxRatesInProducts(products: CatalogProduct[]): number[] {
    const rates = new Set(products.map((p) => Number(p.taxRate)));
    return PREDEFINED_TAX_RATES.map((t) => t.rate).filter((rate) => rates.has(rate));
}

export function sumSelectedValue(products: CatalogProduct[], selectedIds: Set<string>): number {
    return products
        .filter((p) => selectedIds.has(p.id))
        .reduce((sum, p) => sum + Number(p.price), 0);
}

export function buildImportRequest(
    catalog: DemoImportCatalog,
    selectedProductIds: Set<string>,
    overwriteExisting: boolean,
): DemoImportRequest {
    const selected = catalog.products.filter((p) => selectedProductIds.has(p.id));
    if (selected.length === 0) {
        return { selectedCategories: [], overwriteExisting };
    }

    const categories = [...new Set(selected.map((p) => p.category))];
    const allInCategories = catalog.products.filter((p) => categories.includes(p.category));
    const isFullCategorySelection = selected.length === allInCategories.length;

    return {
        selectedCategories: categories,
        overwriteExisting,
        ...(!isFullCategorySelection ? { selectedProductIds: selected.map((p) => p.id) } : {}),
    };
}

export function toggleProductIds(
    current: Set<string>,
    productIds: string[],
    selected: boolean,
): Set<string> {
    const next = new Set(current);
    for (const id of productIds) {
        if (selected) next.add(id);
        else next.delete(id);
    }
    return next;
}
