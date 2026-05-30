import { describe, expect, it } from 'vitest';
import { buildAdminProductsListQueryParams } from '../products';

describe('buildAdminProductsListQueryParams', () => {
    it('maps search and pagination to advanced list query', () => {
        expect(
            buildAdminProductsListQueryParams({
                pageNumber: 2,
                pageSize: 10,
                searchTerm: 'milk',
                searchInName: true,
            }),
        ).toEqual({
            pageNumber: 2,
            pageSize: 10,
            sortBy: undefined,
            sortDirection: undefined,
            searchTerm: 'milk',
            searchInName: true,
            searchInDescription: undefined,
            searchInBarcode: undefined,
            minPrice: undefined,
            maxPrice: undefined,
            stockStatus: undefined,
            minStock: undefined,
            maxStock: undefined,
            taxTypes: undefined,
            isActive: undefined,
            isTaxable: undefined,
            createdFrom: undefined,
            createdTo: undefined,
        });
    });

    it('passes isActive for all and inactive scopes', () => {
        expect(buildAdminProductsListQueryParams({ isActive: 'all' }).isActive).toBe('all');
        expect(buildAdminProductsListQueryParams({ isActive: 'false' }).isActive).toBe('false');
    });

    it('maps legacy name to searchTerm and categoryId to categoryIds', () => {
        const q = buildAdminProductsListQueryParams({
            pageNumber: 1,
            pageSize: 20,
            name: 'tea',
            categoryId: 'cat-1',
            isActive: 'all',
        });
        expect(q.searchTerm).toBe('tea');
        expect(q.categoryIds).toEqual(['cat-1']);
        expect(q.isActive).toBe('all');
    });

    it('passes advanced filter arrays', () => {
        const q = buildAdminProductsListQueryParams({
            taxTypes: [1, 2],
            categoryIds: ['a', 'b'],
            stockStatus: 'LowStock',
            minPrice: 1,
            maxPrice: 9.99,
        });
        expect(q.taxTypes).toEqual([1, 2]);
        expect(q.categoryIds).toEqual(['a', 'b']);
        expect(q.stockStatus).toBe('LowStock');
        expect(q.minPrice).toBe(1);
        expect(q.maxPrice).toBe(9.99);
    });
});
