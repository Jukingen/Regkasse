import type { PosDailyClosingReportDto } from '../services/api/shiftService';

export interface DailyClosingReportLabels {
  title: string;
  date: string;
  register: string;
  cashier: string;
  tseStatus: string;
  sales: string;
  cash: string;
  card: string;
  voucher: string;
  other: string;
  paymentMethodsSection: string;
  transactionBreakdownSection: string;
  breakdownCash: string;
  breakdownCard: string;
  breakdownVoucher: string;
  breakdownCancellations: string;
  breakdownTotal: string;
  cashCount: string;
  difference: string;
  fiscalTotal: string;
  fiscalTax: string;
  taxSection: string;
  tax20: string;
  tax10: string;
  tax0: string;
  transactions: string;
  tseSignature: string;
  previousSignature: string;
  disclaimer: string;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function formatMoney(amount: number, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount);
}

import { formatUserDate } from './dateFormatter';

function formatDate(iso: string, _locale: string): string {
  return formatUserDate(iso) || iso;
}

export function formatDailyClosingReportHtml(
  report: PosDailyClosingReportDto,
  labels: DailyClosingReportLabels,
  formatLocale: string
): string {
  const row = (label: string, value: string) =>
    `<tr><td style="padding:4px 8px 4px 0;color:#555;">${escapeHtml(label)}</td>` +
    `<td style="padding:4px 0;font-weight:600;text-align:right;">${escapeHtml(value)}</td></tr>`;

  const section = (title: string) =>
    `<tr><td colspan="2" style="padding:8px 0 4px;font-weight:700;font-size:11px;">${escapeHtml(title)}</td></tr>`;

  const tseSignature = report.tseSignature?.trim() || '—';
  const previousSignature = report.previousClosingSignature?.trim() || '—';
  const disclaimer = report.snapshotDisclaimerDe || labels.disclaimer;
  const tseStatus = report.tseStatusLabel?.trim() || '—';
  const tseBadgeText = report.tseStatusBadge?.trim() || report.tseStatusLabel?.trim() || '';
  const tax = report.taxBreakdown;
  const payments = report.paymentBreakdown ?? {
    cash: report.totalCash,
    card: report.totalCard,
    voucher: report.totalVoucherRedemptions,
    other: report.totalOtherPaymentMethods,
    total:
      report.totalCash +
      report.totalCard +
      report.totalVoucherRedemptions +
      report.totalOtherPaymentMethods,
  };

  const breakdown = report.transactionBreakdown ?? {
    cash: 0,
    card: 0,
    voucher: 0,
    cancellations: 0,
    total: 0,
  };

  const notes = [
    report.salesFiscalReconciliationNote,
    report.differenceScopeNote,
  ]
    .filter((note): note is string => Boolean(note?.trim()))
    .map(
      (note) =>
        `<p class="note">${escapeHtml(note)}</p>`
    )
    .join('');

  const taxRows = tax
    ? [
        tax.grossAt20 > 0 || tax.taxAt20 > 0
          ? row(labels.tax20, `${formatMoney(tax.grossAt20, formatLocale)} / ${formatMoney(tax.taxAt20, formatLocale)}`)
          : '',
        tax.grossAt10 > 0 || tax.taxAt10 > 0
          ? row(labels.tax10, `${formatMoney(tax.grossAt10, formatLocale)} / ${formatMoney(tax.taxAt10, formatLocale)}`)
          : '',
        tax.grossAt0 > 0 ? row(labels.tax0, formatMoney(tax.grossAt0, formatLocale)) : '',
      ]
        .filter(Boolean)
        .join('')
    : '';

  const tseBadge = tseBadgeText
    ? `<p class="${report.isDemoFiscal ? 'demo-badge' : 'prod-badge'}">${escapeHtml(tseBadgeText)}</p>`
    : '';

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    body { font-family: Arial, sans-serif; font-size: 12px; color: #111; margin: 0; padding: 12px; }
    h1 { font-size: 16px; margin: 0 0 12px; text-align: center; }
    table { width: 100%; border-collapse: collapse; }
    .disclaimer { margin-top: 12px; font-size: 10px; color: #666; line-height: 1.4; white-space: pre-line; }
    .demo-badge { margin-top: 8px; text-align: center; font-weight: 700; color: #c2410c; font-size: 11px; }
    .prod-badge { margin-top: 8px; text-align: center; font-weight: 700; color: #15803d; font-size: 11px; }
    .note { margin: 6px 0 0; font-size: 10px; color: #666; line-height: 1.4; }
    .tse { margin-top: 10px; font-size: 9px; word-break: break-all; line-height: 1.35; }
  </style>
</head>
<body>
  <h1>${escapeHtml(labels.title)}</h1>
  <table>
    ${row(labels.date, formatDate(report.businessDate, formatLocale))}
    ${row(labels.register, report.registerNumber || '—')}
    ${row(labels.cashier, report.cashierName || '—')}
    ${row(labels.tseStatus, tseStatus)}
    ${row(labels.sales, formatMoney(report.totalSales, formatLocale))}
    ${row(labels.fiscalTotal, formatMoney(report.fiscalTotalAmount, formatLocale))}
    ${row(labels.fiscalTax, formatMoney(report.fiscalTotalTaxAmount, formatLocale))}
    ${row(labels.transactions, String(report.fiscalTransactionCount))}
    ${taxRows ? section(labels.taxSection) + taxRows : ''}
    ${section(labels.paymentMethodsSection)}
    ${row(labels.cash, formatMoney(payments.cash, formatLocale))}
    ${row(labels.card, formatMoney(payments.card, formatLocale))}
    ${row(labels.voucher, formatMoney(payments.voucher, formatLocale))}
    ${row(labels.other, formatMoney(payments.other, formatLocale))}
    ${section(labels.transactionBreakdownSection)}
    ${row(labels.breakdownCash, String(breakdown.cash))}
    ${row(labels.breakdownCard, String(breakdown.card))}
    ${row(labels.breakdownVoucher, String(breakdown.voucher))}
    ${row(labels.breakdownCancellations, String(breakdown.cancellations))}
    ${row(labels.breakdownTotal, String(breakdown.total))}
    ${row(labels.cashCount, formatMoney(report.cashCount, formatLocale))}
    ${row(labels.difference, formatMoney(report.difference, formatLocale))}
  </table>
  ${notes}
  <p class="tse"><strong>${escapeHtml(labels.tseSignature)}:</strong> ${escapeHtml(tseSignature)}</p>
  ${report.previousClosingSignature?.trim() ? `<p class="tse"><strong>${escapeHtml(labels.previousSignature)}:</strong> ${escapeHtml(previousSignature)}</p>` : ''}
  ${tseBadge}
  <p class="disclaimer">${escapeHtml(disclaimer)}</p>
</body>
</html>`;
}
