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
        totalVoucherRedemptions: 0,
        totalOtherPaymentMethods: 0,
        cashCount: 80,
        difference: 0,
        fiscalTotalAmount: 120,
        fiscalTotalTaxAmount: 20,
        fiscalTransactionCount: 5,
        tseSignature: 'abc.def.ghi',
        transactionBreakdown: {
          cash: 2,
          card: 1,
          voucher: 0,
          cancellations: 1,
          total: 3,
        },
      },
      {
        title: 'Tagesabschluss',
        date: 'Datum',
        register: 'Kasse',
        cashier: 'Kassierer',
        tseStatus: 'TSE-Status',
        sales: 'Umsatz',
        cash: 'Bar',
        card: 'Karte',
        voucher: 'Gutschein',
        other: 'Sonstige',
        paymentMethodsSection: 'Zahlungsarten',
        transactionBreakdownSection: 'Transaktionen',
        breakdownCash: 'Bar',
        breakdownCard: 'Karte',
        breakdownVoucher: 'Gutschein',
        breakdownCancellations: 'Stornos',
        breakdownTotal: 'Gesamt',
        cashCount: 'Gezählt',
        difference: 'Differenz',
        fiscalTotal: 'Gesamt',
        fiscalTax: 'MwSt',
        taxSection: 'MwSt.',
        tax20: '20%',
        tax10: '10%',
        tax0: '0%',
        transactions: 'Belege',
        tseSignature: 'TSE',
        previousSignature: 'Vorherige TSE',
        disclaimer: 'Hinweis',
      },
      'de-AT'
    );

    expect(html).toContain('Tagesabschluss');
    expect(html).toContain('K1');
    expect(html).toContain('abc.def.ghi');
    expect(html).toContain('Transaktionen');
    expect(html).toContain('>3<');
  });
});
