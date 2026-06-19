import type { PosDailyClosingReportDto } from '../services/api/shiftService';

export interface DailyClosingReportLabels {
  title: string;
  date: string;
  register: string;
  sales: string;
  cash: string;
  card: string;
  cashCount: string;
  difference: string;
  fiscalTotal: string;
  fiscalTax: string;
  transactions: string;
  tseSignature: string;
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

  const tsePreview = report.tseSignature
    ? report.tseSignature.length > 48
      ? `${report.tseSignature.slice(0, 48)}…`
      : report.tseSignature
    : '—';

  const disclaimer = report.snapshotDisclaimerDe || labels.disclaimer;

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    body { font-family: Arial, sans-serif; font-size: 12px; color: #111; margin: 0; padding: 12px; }
    h1 { font-size: 16px; margin: 0 0 12px; text-align: center; }
    table { width: 100%; border-collapse: collapse; }
    .disclaimer { margin-top: 12px; font-size: 10px; color: #666; line-height: 1.4; }
  </style>
</head>
<body>
  <h1>${escapeHtml(labels.title)}</h1>
  <table>
    ${row(labels.date, formatDate(report.businessDate, formatLocale))}
    ${row(labels.register, report.registerNumber || '—')}
    ${row(labels.sales, formatMoney(report.totalSales, formatLocale))}
    ${row(labels.cash, formatMoney(report.totalCash, formatLocale))}
    ${row(labels.card, formatMoney(report.totalCard, formatLocale))}
    ${row(labels.cashCount, formatMoney(report.cashCount, formatLocale))}
    ${row(labels.difference, formatMoney(report.difference, formatLocale))}
    ${row(labels.fiscalTotal, formatMoney(report.fiscalTotalAmount, formatLocale))}
    ${row(labels.fiscalTax, formatMoney(report.fiscalTotalTaxAmount, formatLocale))}
    ${row(labels.transactions, String(report.fiscalTransactionCount))}
    ${row(labels.tseSignature, tsePreview)}
  </table>
  <p class="disclaimer">${escapeHtml(disclaimer)}</p>
</body>
</html>`;
}
