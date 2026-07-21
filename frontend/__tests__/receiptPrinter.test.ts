/**
 * Receipt template tests: MwSt table and totals match backend (gross model).
 * Interspar-style: 10.70 gross, 10% → net 9.73, tax 0.97.
 * OMV-style: 11.89 gross, 20% → net 9.91, tax 1.98.
 */
import { describe, expect, it } from '@jest/globals';

import { formatReceiptHtml } from '../services/receiptFormatter';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import { normalizeReceiptDto } from '../utils/normalizeReceiptDto';

const baseReceipt = (overrides: Partial<ReceiptDTO> = {}): ReceiptDTO => ({
  receiptId: 'test-id',
  receiptNumber: '4651',
  date: new Date().toISOString(),
  cashierId: 'user-196710',
  cashierDisplayName: 'Kassier 196710',
  company: { name: 'Test Store', address: 'Graz', taxNumber: 'ATU12345678' },
  kassenID: 'KASSE 031',
  items: [],
  taxRates: [],
  subtotal: 0,
  taxAmount: 0,
  grandTotal: 0,
  payments: [{ method: 'cash', amount: 0, tendered: 0, change: 0 }],
  signature: {
    algorithm: 'ES256',
    value: '',
    serialNumber: '',
    timestamp: '',
    qrData: '',
  },
  ...overrides,
});

describe('receiptPrinter', () => {
  it('Interspar-style: 10.70 gross, 10% → net 9.73, tax 0.97 in MwSt table and SUMME', () => {
    const data: ReceiptDTO = baseReceipt({
      items: [
        { name: 'SVELACHSFILET', quantity: 1, unitPrice: 7, totalPrice: 7, taxRate: 10 },
        { name: 'SALAT KLEINER TELLER', quantity: 1, unitPrice: 3.7, totalPrice: 3.7, taxRate: 10 },
      ],
      taxRates: [{ rate: 10, netAmount: 9.73, taxAmount: 0.97, grossAmount: 10.7 }],
      subtotal: 9.73,
      taxAmount: 0.97,
      grandTotal: 10.7,
      payments: [{ method: 'cash', amount: 10.7, tendered: 50, change: 39.3 }],
    });
    const html = formatReceiptHtml(data);
    expect(html).toContain('9,73');
    expect(html).toContain('0,97');
    expect(html).toContain('10,70');
    expect(html).toContain('SUMME / Gesamtbetrag');
    expect(html).toContain('EUR 10,70');
    expect(html).toContain('MwSt%');
    expect(html).toContain('excl.');
    expect(html).toContain('MWST.');
    expect(html).toContain('Incl.');
    expect(html).toContain('Registrierkassensicherheitsverordnung');
  });

  it('OMV-style: 11.89 gross, 20% → net 9.91, tax 1.98 in MwSt table and SUMME', () => {
    const data: ReceiptDTO = baseReceipt({
      items: [
        { name: 'TOP NORMAL', quantity: 1, unitPrice: 16.99, totalPrice: 11.89, taxRate: 20 },
      ],
      taxRates: [{ rate: 20, netAmount: 9.91, taxAmount: 1.98, grossAmount: 11.89 }],
      subtotal: 9.91,
      taxAmount: 1.98,
      grandTotal: 11.89,
      payments: [{ method: 'cash', amount: 11.89, tendered: 20, change: 8.11 }],
    });
    const html = formatReceiptHtml(data);
    expect(html).toContain('9,91');
    expect(html).toContain('1,98');
    expect(html).toContain('11,89');
    expect(html).toContain('SUMME / Gesamtbetrag');
    expect(html).toContain('EUR 11,89');
  });

  it('sorts tax table by rate ascending (10% before 20%)', () => {
    const data: ReceiptDTO = baseReceipt({
      taxRates: [
        { rate: 20, netAmount: 8.33, taxAmount: 1.67, grossAmount: 10 },
        { rate: 10, netAmount: 9.09, taxAmount: 0.91, grossAmount: 10 },
      ],
      subtotal: 17.42,
      taxAmount: 2.58,
      grandTotal: 20,
      payments: [{ method: 'cash', amount: 20, tendered: 20, change: 0 }],
    });
    const html = formatReceiptHtml(data);
    const firstRateIdx = html.indexOf('10,00%');
    const secondRateIdx = html.indexOf('20,00%');
    expect(firstRateIdx).toBeGreaterThan(-1);
    expect(secondRateIdx).toBeGreaterThan(-1);
    expect(firstRateIdx).toBeLessThan(secondRateIdx);
  });

  it('shows DEMO label when backend sends dev/staging rksvFooterLabel', () => {
    const data = baseReceipt({ grandTotal: 10, rksvFooterLabel: 'DEMO / NICHT FISKAL' });
    const html = formatReceiptHtml(data);
    expect(html).toContain('DEMO');
    expect(html).toContain('NICHT FISKAL');
  });

  it('shows production RKSV footer label by default', () => {
    const data = baseReceipt({ grandTotal: 10 });
    const html = formatReceiptHtml(data);
    expect(html).toContain('RKSV-konform');
    expect(html).not.toContain('NICHT FISKAL');
  });

  it('ignores isDemoFiscal when production rksvFooterLabel is set', () => {
    const data = baseReceipt({
      grandTotal: 10,
      rksvFooterLabel: 'RKSV-konform',
    });
    const html = formatReceiptHtml(data, { isDemoFiscal: true });
    expect(html).not.toContain('NICHT FISKAL');
    expect(html).toContain('RKSV-konform');
  });

  it('renders TSE signature from backend SignatureValue mapping', () => {
    const jws = 'eyJhbGciOiJFUzI1NiJ9.eyJ.test.sign';
    const data = baseReceipt({
      grandTotal: 10,
      signature: {
        algorithm: 'ES256',
        value: jws,
        serialNumber: 'TSE-001',
        timestamp: '2026-07-12T12:00:00',
        qrData: '',
      },
    });
    const html = formatReceiptHtml(data);
    expect(html).toContain('TSE-Signatur:');
    expect(html).toContain(jws);
  });

  it('shows unavailable TSE signature message when signature is missing', () => {
    const data = baseReceipt({ grandTotal: 10 });
    const html = formatReceiptHtml(data);
    expect(html).toContain('TSE-Signatur: nicht verfügbar');
  });

  it('normalizeReceiptDto maps PascalCase SignatureValue and Company from backend', () => {
    const dto = normalizeReceiptDto({
      ReceiptNumber: '1001',
      CashierDisplayName: 'Max Mustermann',
      Company: { Name: 'Cafe Wien', Address: 'Wien', TaxNumber: 'ATU99999999' },
      Signature: { SignatureValue: 'eyJ.eyJ.sign' },
      Items: [],
      TaxRates: [],
      SubTotal: 0,
      TaxAmount: 0,
      GrandTotal: 0,
      Payments: [],
    });
    expect(dto.company.name).toBe('Cafe Wien');
    expect(dto.signature.value).toBe('eyJ.eyJ.sign');
    expect(dto.cashierDisplayName).toBe('Max Mustermann');
  });

  it('45.18 gross, 20% → MwSt 7.53 in receipt (cart/receipt consistency)', () => {
    const data: ReceiptDTO = baseReceipt({
      items: [{ name: 'Artikel', quantity: 1, unitPrice: 45.18, totalPrice: 45.18, taxRate: 20 }],
      taxRates: [{ rate: 20, netAmount: 37.65, taxAmount: 7.53, grossAmount: 45.18 }],
      subtotal: 37.65,
      taxAmount: 7.53,
      grandTotal: 45.18,
      payments: [{ method: 'cash', amount: 45.18, tendered: 50, change: 4.82 }],
    });
    const html = formatReceiptHtml(data);
    expect(html).toContain('7,53');
    expect(html).toContain('37,65');
    expect(html).toContain('45,18');
    expect(html).toContain('SUMME / Gesamtbetrag');
    expect(html).toContain('EUR 45,18');
  });
});
