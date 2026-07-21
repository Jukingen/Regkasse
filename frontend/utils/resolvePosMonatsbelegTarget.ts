/** Minimal status fields needed to pick the Monatsbeleg target month. */
export type PosMonatsbelegTargetStatus = {
  nextRequiredMonth: string | null;
  missingMonths: {
    year: number;
    month: number;
    isOverdue: boolean;
  }[];
};

/** Europe/Vienna calendar year and month (matches server Monatsbeleg guard). */
export function getViennaYearMonth(now: Date = new Date()): { year: number; month: number } {
  const fmt = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Vienna',
    year: 'numeric',
    month: '2-digit',
  });
  const parts = fmt.formatToParts(now);
  const year = Number(parts.find((p) => p.type === 'year')?.value ?? '0');
  const month = Number(parts.find((p) => p.type === 'month')?.value ?? '0');
  return { year, month };
}

/**
 * Resolve which Vienna calendar month the POS should create next.
 * Prefers the earliest overdue missing month, then any missing month / nextRequiredMonth,
 * otherwise the current Vienna month (same default as the session block modal).
 */
export function resolvePosMonatsbelegTarget(status?: PosMonatsbelegTargetStatus | null): {
  year: number;
  month: number;
} {
  const missing = status?.missingMonths;
  if (missing && missing.length > 0) {
    const overdue = missing.find((m) => m.isOverdue);
    const pick = overdue ?? missing[0];
    return { year: pick.year, month: pick.month };
  }

  const next = status?.nextRequiredMonth?.trim();
  if (next) {
    const match = /^(\d{4})-(\d{2})$/.exec(next);
    if (match) {
      const year = Number(match[1]);
      const month = Number(match[2]);
      if (year > 0 && month >= 1 && month <= 12) {
        return { year, month };
      }
    }
  }

  return getViennaYearMonth();
}
