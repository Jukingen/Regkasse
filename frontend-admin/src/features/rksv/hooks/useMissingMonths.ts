'use client';

import { useMemo } from 'react';

import { useMonatsbelegStatus } from '@/features/rksv/hooks/useMonatsbeleg';
import { formatViennaYearMonth, getViennaCalendarYearMonth } from '@/shared/utils/viennaCalendar';

export type CurrentMonthMissingEntry = {
  cashRegisterId: string;
  year: number;
  month: number;
  yearMonth: string;
  isOverdue: boolean;
};

type UseMissingMonthsOptions = {
  enabled?: boolean;
};

/**
 * Registers that still need a Monatsbeleg for the current Vienna calendar month.
 * Historical gaps in {@link MonatsbelegStatusDto.missingMonths} are excluded here — use Sonderbelege or {@link CreateMonatsbelegModal} with force for past months.
 */
export function useMissingMonths(options?: UseMissingMonthsOptions) {
  const enabled = options?.enabled ?? true;
  const { year, month } = useMemo(() => getViennaCalendarYearMonth(), []);
  const yearMonth = formatViennaYearMonth(year, month);

  const query = useMonatsbelegStatus({ enabled });

  const missingMonths = useMemo((): CurrentMonthMissingEntry[] => {
    const entries: CurrentMonthMissingEntry[] = [];
    for (const item of query.data ?? []) {
      const cashRegisterId = item.cashRegisterId?.trim();
      if (!cashRegisterId || !item.status || item.status.currentMonthExists) continue;
      entries.push({
        cashRegisterId,
        year,
        month,
        yearMonth,
        isOverdue: item.status.currentMonthOverdue ?? false,
      });
    }
    return entries;
  }, [query.data, month, year, yearMonth]);

  return {
    ...query,
    missingMonths,
    currentYearMonth: yearMonth,
    viennaYear: year,
    viennaMonth: month,
  };
}
