import { describe, expect, it } from 'vitest';
import deProducts from '@/i18n/locales/de/products.json';
import {
    PRODUCT_LIST_STATUS_FILTER_VALUES,
    activeFilterFromUrl,
    listIsActiveQueryParam,
    shouldClearProductListStatusQueryParam,
} from '../productListStatusQuery';

describe('productListStatusQuery', () => {
    describe('activeFilterFromUrl', () => {
        it('maps all / active / inactive and defaults invalid to active', () => {
            expect(activeFilterFromUrl(undefined)).toBe('active');
            expect(activeFilterFromUrl('')).toBe('active');
            expect(activeFilterFromUrl('active')).toBe('active');
            expect(activeFilterFromUrl('ACTIVE')).toBe('active');
            expect(activeFilterFromUrl('inactive')).toBe('inactive');
            expect(activeFilterFromUrl('all')).toBe('all');
            expect(activeFilterFromUrl('bogus')).toBe('active');
        });
    });

    describe('listIsActiveQueryParam', () => {
        it('maps UI filter to API isActive query contract', () => {
            expect(listIsActiveQueryParam('active')).toBeUndefined();
            expect(listIsActiveQueryParam('all')).toBe('all');
            expect(listIsActiveQueryParam('inactive')).toBe('false');
        });
    });

    describe('shouldClearProductListStatusQueryParam', () => {
        it('keeps undefined; clears empty or invalid', () => {
            expect(shouldClearProductListStatusQueryParam(undefined)).toBe(false);
            expect(shouldClearProductListStatusQueryParam('')).toBe(true);
            expect(shouldClearProductListStatusQueryParam('   ')).toBe(true);
            expect(shouldClearProductListStatusQueryParam('nope')).toBe(true);
            expect(shouldClearProductListStatusQueryParam('active')).toBe(false);
            expect(shouldClearProductListStatusQueryParam('inactive')).toBe(false);
            expect(shouldClearProductListStatusQueryParam('all')).toBe(false);
        });
    });

    describe('UI i18n contract (de default locale)', () => {
        it('exposes German labels for each filter value', () => {
            expect(PRODUCT_LIST_STATUS_FILTER_VALUES).toEqual(['all', 'active', 'inactive']);
            expect(deProducts.page.filterAll).toBeTruthy();
            expect(deProducts.page.filterActive).toBeTruthy();
            expect(deProducts.page.filterInactive).toBeTruthy();
            expect(deProducts.page.filterLabel).toBeTruthy();
        });
    });
});
