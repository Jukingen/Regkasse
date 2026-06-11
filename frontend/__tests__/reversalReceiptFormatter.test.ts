import { describe, expect, it } from '@jest/globals';

import {
  formatMoneyDe,
  formatRefundReceiptHtml,
  formatStornoReceiptHtml,
} from '../services/reversalReceiptFormatter';

describe('reversalReceiptFormatter', () => {
  const original = {
    receiptNumber: 'R-1001',
    createdAt: '2026-06-11T10:00:00.000Z',
    totalAmount: 25.5,
  };

  it('formatStornoReceiptHtml shows original and storno details', () => {
    const html = formatStornoReceiptHtml(
      {
        receiptNumber: 'R-1002',
        createdAt: '2026-06-11T11:00:00.000Z',
        totalAmount: 25.5,
        stornoReasonText: 'Kunde hat storniert',
      },
      original
    );
    expect(html).toContain('STORNO BELEG');
    expect(html).toContain('R-1001');
    expect(html).toContain('R-1002');
    expect(html).toContain('Kunde hat storniert');
    expect(html).toContain('Neuer Gesamtbetrag');
    expect(html).toContain('0,00');
  });

  it('formatRefundReceiptHtml shows remaining amount after partial refund', () => {
    const html = formatRefundReceiptHtml(
      {
        receiptNumber: 'R-2002',
        createdAt: '2026-06-11T12:00:00.000Z',
        totalAmount: 10,
        refundReason: 'Falsches Produkt',
      },
      original
    );
    expect(html).toContain('ERSTATTUNGSBELEG');
    expect(html).toContain('Verbleibender Betrag');
    expect(html).toContain('Falsches Produkt');
    expect(html).toContain('15,50');
  });

  it('formatMoneyDe uses Austrian EUR formatting', () => {
    expect(formatMoneyDe(12.5)).toContain('12,50');
    expect(formatMoneyDe(12.5)).toContain('€');
  });
});
