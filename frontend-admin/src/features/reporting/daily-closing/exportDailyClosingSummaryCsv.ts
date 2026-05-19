import type { DailyClosingSummaryDto } from '@/api/generated/model';
import { downloadCsvText, rowsToCsv } from '@/shared/utils/csv';

/** Localized CSV header labels for the daily-closing summary row. */
export type DailyClosingCsvColumnLabels = {
  date: string;
  register: string;
  openingBalance: string;
  closingBalance: string;
  totalSales: string;
  cashCount: string;
  difference: string;
};

export type DailyClosingSummaryCsvRow = {
  date: string;
  register: string;
  openingBalance: number | '';
  closingBalance: number | '';
  totalSales: number;
  cashCount: number;
  difference: number | '';
};

/**
 * Maps API snapshot to one CSV data row.
 * Opening/closing balance and physical cash difference are not in DailyClosingSummaryDto yet — left empty.
 */
export function dailyClosingSummaryToCsvRow(
  data: DailyClosingSummaryDto,
  meta: { dateParam: string; registerLabel: string },
): DailyClosingSummaryCsvRow {
  const date =
    data.businessDate != null && data.businessDate !== ''
      ? String(data.businessDate).slice(0, 10)
      : meta.dateParam;

  return {
    date,
    register: meta.registerLabel,
    openingBalance: '',
    closingBalance: '',
    totalSales: data.totalSales ?? 0,
    cashCount: data.totalCash ?? 0,
    difference: '',
  };
}

export function dailyClosingSummaryToCsvMatrix(
  data: DailyClosingSummaryDto,
  meta: { dateParam: string; registerLabel: string },
  labels: DailyClosingCsvColumnLabels,
): unknown[][] {
  const row = dailyClosingSummaryToCsvRow(data, meta);
  return [
    [
      labels.date,
      labels.register,
      labels.openingBalance,
      labels.closingBalance,
      labels.totalSales,
      labels.cashCount,
      labels.difference,
    ],
    [
      row.date,
      row.register,
      row.openingBalance,
      row.closingBalance,
      row.totalSales,
      row.cashCount,
      row.difference,
    ],
  ];
}

export function convertDailyClosingSummaryToCsv(
  data: DailyClosingSummaryDto,
  meta: { dateParam: string; registerLabel: string },
  labels: DailyClosingCsvColumnLabels,
): string {
  return rowsToCsv(dailyClosingSummaryToCsvMatrix(data, meta, labels));
}

export function exportDailyClosingSummaryCsv(
  data: DailyClosingSummaryDto,
  meta: { dateParam: string; registerSlug: string },
  labels: DailyClosingCsvColumnLabels,
): void {
  const registerPart = meta.registerSlug.replace(/[^\w-]+/g, '_').slice(0, 40) || 'all';
  const fileName = `daily-closing_${meta.dateParam}_${registerPart}.csv`;
  const csv = convertDailyClosingSummaryToCsv(
    data,
    { dateParam: meta.dateParam, registerLabel: meta.registerSlug },
    labels,
  );
  downloadCsvText(csv, fileName);
}
