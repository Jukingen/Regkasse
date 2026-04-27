import { describe, expect, it } from 'vitest';
import type { Invoice } from '@/api/generated/model/invoice';
import {
    formatInvoiceDataProvenanceForDisplay,
    readOptionalInvoiceDataProvenance,
} from '@/shared/contract/invoiceDetailResponseExtensions';

function minimalInvoice(overrides: Partial<Invoice> = {}): Invoice {
    return {
        cashRegisterId: '',
        companyAddress: '',
        companyName: '',
        companyTaxNumber: '',
        createdAt: '',
        dueDate: '',
        invoiceDate: '',
        invoiceNumber: 'X',
        kassenId: '',
        paidAmount: 0,
        remainingAmount: 0,
        subtotal: 0,
        taxAmount: 0,
        taxDetails: {},
        totalAmount: 0,
        tseSignature: '',
        tseTimestamp: '',
        status: 0,
        ...overrides,
    };
}

describe('readOptionalInvoiceDataProvenance', () => {
    it('returns undefined when key absent or not a non-empty string', () => {
        expect(readOptionalInvoiceDataProvenance(minimalInvoice())).toBeUndefined();
        expect(
            readOptionalInvoiceDataProvenance({
                ...minimalInvoice(),
                invoiceDataProvenance: null,
            } as Invoice),
        ).toBeUndefined();
        expect(
            readOptionalInvoiceDataProvenance({
                ...minimalInvoice(),
                invoiceDataProvenance: 1,
            } as unknown as Invoice),
        ).toBeUndefined();
    });

    it('returns trimmed string when JSON carries invoiceDataProvenance (response-only vs Orval Invoice)', () => {
        const inv = {
            ...minimalInvoice(),
            invoiceDataProvenance: '  DerivedFromPayment  ',
        } as Invoice;
        expect(readOptionalInvoiceDataProvenance(inv)).toBe('DerivedFromPayment');
    });
});

describe('formatInvoiceDataProvenanceForDisplay', () => {
    it('maps known backend discriminators to German operator copy', () => {
        expect(formatInvoiceDataProvenanceForDisplay('Persisted')).toMatch(/Persistiert/i);
        expect(formatInvoiceDataProvenanceForDisplay('DerivedFromPayment')).toMatch(/Zahlung/i);
    });

    it('passes through unknown raw values', () => {
        expect(formatInvoiceDataProvenanceForDisplay('CustomFlag')).toBe('CustomFlag');
    });
});
