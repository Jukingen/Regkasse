import type { DemoProductImportResult } from '@/api/admin/products';

const euroFormatter = new Intl.NumberFormat('de-DE', {
    style: 'currency',
    currency: 'EUR',
});

export function resolveImportedProductCount(result: DemoProductImportResult): number {
    if (result.importedProductCount != null) {
        return result.importedProductCount;
    }
    return result.created + result.updated;
}

export function resolveCategoriesCreated(result: DemoProductImportResult): number {
    if (result.categoriesCreated != null) {
        return result.categoriesCreated;
    }
    return result.selectedCategoryCount ?? 0;
}

export function formatAverageImportedPrice(result: DemoProductImportResult): string | null {
    const avg = result.averageImportedPrice;
    if (avg == null || avg <= 0) {
        return null;
    }
    return euroFormatter.format(avg);
}
