export type DailyClosingTodayTone = 'closed' | 'open' | 'empty';

export function resolveDailyClosingTodayTone(today: {
  isClosed?: boolean;
  transactionCount?: number;
}): DailyClosingTodayTone {
  if (today.isClosed) return 'closed';
  if ((today.transactionCount ?? 0) > 0) return 'open';
  return 'empty';
}

export function weekClosingPercent(closedDays: number, totalDays: number): number {
  if (totalDays <= 0) return 0;
  return Math.round((closedDays / totalDays) * 100);
}
