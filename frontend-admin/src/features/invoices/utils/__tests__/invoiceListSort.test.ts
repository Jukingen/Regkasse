import { describe, expect, it } from 'vitest';
import { coerceInvoiceListSortField } from '@/features/invoices/types';

describe('coerceInvoiceListSortField', () => {
    it('accepts known API sort keys', () => {
        expect(coerceInvoiceListSortField('invoiceNumber')).toBe('invoiceNumber');
    });

    it('falls back for unknown keys (no silent pass-through)', () => {
        expect(coerceInvoiceListSortField('maliciousField')).toBe('invoiceDate');
        expect(coerceInvoiceListSortField(undefined)).toBe('invoiceDate');
    });
});
