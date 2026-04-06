import { describe, expect, it } from 'vitest';
import { buildAdminProductsListQueryParams } from '../products';

describe('buildAdminProductsListQueryParams', () => {
    it('omits isActive when listing active-only (default API)', () => {
        expect(
            buildAdminProductsListQueryParams({
                pageNumber: 2,
                pageSize: 10,
                name: 'milk',
            })
        ).toEqual({
            pageNumber: 2,
            pageSize: 10,
            categoryId: undefined,
            name: 'milk',
            isActive: undefined,
        });
    });

    it('passes isActive for all and inactive scopes', () => {
        expect(buildAdminProductsListQueryParams({ isActive: 'all' }).isActive).toBe('all');
        expect(buildAdminProductsListQueryParams({ isActive: 'false' }).isActive).toBe('false');
    });

    it('combines search name with isActive', () => {
        const q = buildAdminProductsListQueryParams({
            pageNumber: 1,
            pageSize: 20,
            name: 'tea',
            isActive: 'all',
        });
        expect(q.name).toBe('tea');
        expect(q.isActive).toBe('all');
    });

    it('passes pagination with inactive filter', () => {
        const q = buildAdminProductsListQueryParams({
            pageNumber: 3,
            pageSize: 5,
            isActive: 'false',
        });
        expect(q.pageNumber).toBe(3);
        expect(q.pageSize).toBe(5);
        expect(q.isActive).toBe('false');
    });
});
