import { describe, expect, it } from 'vitest';

import {
  buildInvoiceListFileName,
  buildInvoicePdfFileName,
  sanitizeInvoiceFileSegment,
} from '../invoiceExportFileName';

describe('buildInvoicePdfFileName', () => {
  it('uses canonical pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildInvoicePdfFileName('cafe', 'k1', '45', at)).toBe(
      'invoice_cafe_k1_20260722_143022_45.pdf'
    );
  });
});

describe('buildInvoiceListFileName', () => {
  it('uses canonical csv pattern', () => {
    const at = new Date(2026, 6, 22, 14, 30, 22);
    expect(buildInvoiceListFileName('cafe', '2026-07-01', '2026-07-22', 'csv', at)).toBe(
      'invoices_cafe_20260701_20260722_20260722_143022.csv'
    );
  });

  it('supports excel extension and missing dates', () => {
    const at = new Date(2026, 0, 2, 3, 4, 5);
    expect(buildInvoiceListFileName('cafe', null, null, 'xlsx', at)).toBe(
      'invoices_cafe_all_all_20260102_030405.xlsx'
    );
  });
});

describe('sanitizeInvoiceFileSegment', () => {
  it('falls back when empty', () => {
    expect(sanitizeInvoiceFileSegment(null, 'tenant')).toBe('tenant');
    expect(sanitizeInvoiceFileSegment('***', 'tenant')).toBe('tenant');
  });
});
