import {
    formatAverageImportedPrice,
    resolveCategoriesCreated,
    resolveImportedProductCount,
} from '../demoImportSummary';

describe('demoImportSummary', () => {
    it('prefers server importedProductCount', () => {
        expect(
            resolveImportedProductCount({ success: true, created: 1, updated: 2, skipped: 0, importedProductCount: 10 }),
        ).toBe(10);
    });

    it('falls back to created + updated', () => {
        expect(resolveImportedProductCount({ success: true, created: 3, updated: 4, skipped: 1 })).toBe(7);
    });

    it('formats average price in EUR', () => {
        expect(
            formatAverageImportedPrice({ success: true, created: 0, updated: 0, skipped: 0, averageImportedPrice: 8.45 }),
        ).toMatch(/8,45/);
    });

    it('uses categoriesCreated when present', () => {
        expect(
            resolveCategoriesCreated({
                success: true,
                created: 0,
                updated: 0,
                skipped: 0,
                categoriesCreated: 15,
                selectedCategoryCount: 99,
            }),
        ).toBe(15);
    });
});
