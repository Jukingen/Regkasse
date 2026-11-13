export type HistoryDayKindFilter = 'all' | 'normal' | 'empty';

export type DailyClosingHistoryKindFields = {
  isEmpty?: boolean;
  dayKind?: string | null;
  closingType?: string | null;
  transactionCount?: number;
};

export function isEmptyDailyClosing(row: DailyClosingHistoryKindFields): boolean {
  if (row.isEmpty === true) return true;
  if (row.dayKind?.toLowerCase() === 'empty') return true;
  return row.closingType === 'Daily' && (row.transactionCount ?? 0) === 0;
}

export function filterHistoryByDayKind<T extends DailyClosingHistoryKindFields>(
  rows: T[],
  filter: HistoryDayKindFilter
): T[] {
  if (filter === 'all') return rows;
  return rows.filter((row) => {
    const empty = isEmptyDailyClosing(row);
    return filter === 'empty' ? empty : !empty;
  });
}

