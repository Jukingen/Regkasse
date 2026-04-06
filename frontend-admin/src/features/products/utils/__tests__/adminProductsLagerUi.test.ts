import { describe, expect, it } from 'vitest';
import { parseAdminProductsLagerUiEnv } from '../adminProductsLagerUi';

describe('adminProductsLagerUi', () => {
    describe('parseAdminProductsLagerUiEnv', () => {
        it('defaults to visible when unset or empty (backward compatible)', () => {
            expect(parseAdminProductsLagerUiEnv(undefined)).toBe(true);
            expect(parseAdminProductsLagerUiEnv('')).toBe(true);
            expect(parseAdminProductsLagerUiEnv('   ')).toBe(true);
        });

        it('hides for common false tokens (case-insensitive)', () => {
            expect(parseAdminProductsLagerUiEnv('false')).toBe(false);
            expect(parseAdminProductsLagerUiEnv('FALSE')).toBe(false);
            expect(parseAdminProductsLagerUiEnv('0')).toBe(false);
            expect(parseAdminProductsLagerUiEnv('no')).toBe(false);
            expect(parseAdminProductsLagerUiEnv('off')).toBe(false);
        });

        it('shows for other explicit values', () => {
            expect(parseAdminProductsLagerUiEnv('true')).toBe(true);
            expect(parseAdminProductsLagerUiEnv('1')).toBe(true);
            expect(parseAdminProductsLagerUiEnv('yes')).toBe(true);
        });
    });
});
