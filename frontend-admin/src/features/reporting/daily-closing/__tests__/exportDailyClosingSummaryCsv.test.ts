import { describe, expect, it } from 'vitest';
import {
  convertDailyClosingSummaryToCsv,
  dailyClosingSummaryToCsvRow,
} from '@/features/reporting/daily-closing/exportDailyClosingSummaryCsv';
import type { DailyClosingSummaryDto } from '@/api/generated/model';

const labels = {
  date: 'Date',
  register: 'Register',
  openingBalance: 'Opening Balance',
  closingBalance: 'Closing Balance',
  totalSales: 'Total Sales',
  cashCount: 'Cash Count',
  difference: 'Difference',
};

describe('dailyClosingSummary CSV', () => {
  it('maps summary fields to the seven export columns', () => {
    const data: DailyClosingSummaryDto = {
      businessDate: '2026-05-18',
      totalSales: 1200.5,
      totalCash: 400.25,
    };
    expect(
      dailyClosingSummaryToCsvRow(data, {
        dateParam: '2026-05-18',
        registerLabel: 'K1',
      }),
    ).toEqual({
      date: '2026-05-18',
      register: 'K1',
      openingBalance: '',
      closingBalance: '',
      totalSales: 1200.5,
      cashCount: 400.25,
      difference: '',
    });
  });

  it('renders header row and one data row', () => {
    const csv = convertDailyClosingSummaryToCsv(
      { totalSales: 100, totalCash: 30 },
      { dateParam: '2026-05-18', registerLabel: 'All' },
      labels,
    );
    const lines = csv.split('\n');
    expect(lines[0]).toBe(
      'Date,Register,Opening Balance,Closing Balance,Total Sales,Cash Count,Difference',
    );
    expect(lines[1]).toBe('2026-05-18,All,,,100,30,');
  });

  it('escapes commas in register label', () => {
    const csv = convertDailyClosingSummaryToCsv(
      { totalSales: 1, totalCash: 1 },
      { dateParam: '2026-05-18', registerLabel: 'Kasse A, West' },
      labels,
    );
    expect(csv).toContain('"Kasse A, West"');
  });
});
