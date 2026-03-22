import { describe, expect, it } from 'vitest';
import { normalizeInvoiceItemsForDisplay } from '@/shared/contract/invoiceInvoiceItemsDisplay';

describe('normalizeInvoiceItemsForDisplay', () => {
    it('returns empty rows for null', () => {
        expect(normalizeInvoiceItemsForDisplay(null)).toEqual({ kind: 'rows', rows: [] });
    });

    it('parses JSON array string', () => {
        const r = normalizeInvoiceItemsForDisplay('[{"a":1}]');
        expect(r.kind).toBe('rows');
        if (r.kind === 'rows') expect(r.rows).toEqual([{ a: 1 }]);
    });

    it('returns parse_error for invalid JSON string', () => {
        const r = normalizeInvoiceItemsForDisplay('{');
        expect(r.kind).toBe('parse_error');
    });

    it('wraps non-array JSON object as single row', () => {
        const r = normalizeInvoiceItemsForDisplay('{"x":1}');
        expect(r.kind).toBe('rows');
        if (r.kind === 'rows') expect(r.rows).toEqual([{ x: 1 }]);
    });

    it('passes through array', () => {
        const r = normalizeInvoiceItemsForDisplay([{ id: '1' }]);
        expect(r.kind).toBe('rows');
        if (r.kind === 'rows') expect(r.rows).toHaveLength(1);
    });
});
