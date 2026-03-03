/**
 * Receipt template tests: MwSt table and totals match backend (gross model).
 * Interspar-style: 10.70 gross, 10% → net 9.73, tax 0.97.
 * OMV-style: 11.89 gross, 20% → net 9.91, tax 1.98.
 */
import { formatReceiptHtml } from '../services/receiptFormatter';
import type { ReceiptDTO } from '../types/ReceiptDTO';

const baseReceipt = (overrides: Partial<ReceiptDTO> = {}): ReceiptDTO => ({
  receiptId: 'test-id',
  receiptNumber: '4651',
  date: new Date().toISOString(),
  cashierName: 'Kassier 196710',
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
      items: [{ name: 'SVELACHSFILET', quantity: 1, unitPrice: 7, totalPrice: 7, taxRate: 10 }, { name: 'SALAT KLEINER TELLER', quantity: 1, unitPrice: 3.70, totalPrice: 3.70, taxRate: 10 }],
      taxRates: [{ rate: 10, netAmount: 9.73, taxAmount: 0.97, grossAmount: 10.70 }],
      subtotal: 9.73,
      taxAmount: 0.97,
      grandTotal: 10.70,
      payments: [{ method: 'cash', amount: 10.70, tendered: 50, change: 39.30 }],
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
      items: [{ name: 'TOP NORMAL', quantity: 1, unitPrice: 16.99, totalPrice: 11.89, taxRate: 20 }],
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

  it('shows DEMO label when isDemoFiscal is true', () => {
    const data = baseReceipt({ grandTotal: 10 });
    const html = formatReceiptHtml(data, { isDemoFiscal: true });
    expect(html).toContain('DEMO');
    expect(html).toContain('NICHT FISKAL');
  });
});
