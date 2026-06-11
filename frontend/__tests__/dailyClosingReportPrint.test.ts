import { describe, expect, it } from '@jest/globals';

import { formatDailyClosingReportHtml } from '../utils/dailyClosingReportFormat';

describe('formatDailyClosingReportHtml', () => {
  it('includes title and formatted amounts', () => {
    const html = formatDailyClosingReportHtml(
      {
        businessDate: '2026-06-11T00:00:00Z',
        registerNumber: 'K1',
        totalSales: 120,
        totalCash: 80,
        totalCard: 40,
        cashCount: 80,
        difference: 0,
        fiscalTotalAmount: 120,
        fiscalTotalTaxAmount: 20,
        fiscalTransactionCount: 5,
        tseSignature: 'abc.def.ghi',
      },
      {
        title: 'Tagesabschluss',
        date: 'Datum',
        register: 'Kasse',
        sales: 'Umsatz',
        cash: 'Bar',
        card: 'Karte',
        cashCount: 'Gezählt',
        difference: 'Differenz',
        fiscalTotal: 'Gesamt',
        fiscalTax: 'MwSt',
        transactions: 'Belege',
        tseSignature: 'TSE',
        disclaimer: 'Hinweis',
      },
      'de-AT'
    );

    expect(html).toContain('Tagesabschluss');
    expect(html).toContain('K1');
    expect(html).toContain('abc.def.ghi');
  });
});
