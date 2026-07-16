/** Vienna (Europe/Vienna) calendar helpers for RKSV Monatsbeleg compliance. */

export function getViennaCalendarYear(now: Date = new Date()): number {
    const fmt = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Vienna', year: 'numeric' });
    const y = fmt.formatToParts(now).find((p) => p.type === 'year')?.value;
    return y ? Number(y) : now.getUTCFullYear();
}

export function getViennaCalendarYearMonth(now: Date = new Date()): { year: number; month: number } {
    const fmt = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Europe/Vienna',
        year: 'numeric',
        month: '2-digit',
    });
    const parts = fmt.formatToParts(now);
    const year = Number(parts.find((p) => p.type === 'year')?.value) || now.getUTCFullYear();
    const month = Number(parts.find((p) => p.type === 'month')?.value) || 1;
    return { year, month };
}

export function formatViennaYearMonth(year: number, month: number): string {
    return `${year}-${String(month).padStart(2, '0')}`;
}

/** Positive = past Vienna month, 0 = current, negative = future. */
export function getMonthDifference(year: number, month: number, now: Date = new Date()): number {
    const { year: viennaYear, month: viennaMonth } = getViennaCalendarYearMonth(now);
    const currentAnchor = viennaYear * 12 + (viennaMonth - 1);
    const targetAnchor = year * 12 + (month - 1);
    return currentAnchor - targetAnchor;
}

/** Current calendar day components in Europe/Vienna. */
export function getViennaCalendarDate(now: Date = new Date()): { year: number; month: number; day: number } {
    const fmt = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Europe/Vienna',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
    const parts = fmt.formatToParts(now);
    const year = Number(parts.find((p) => p.type === 'year')?.value) || now.getUTCFullYear();
    const month = Number(parts.find((p) => p.type === 'month')?.value) || 1;
    const day = Number(parts.find((p) => p.type === 'day')?.value) || 1;
    return { year, month, day };
}

/** YYYY-MM-DD for the current Vienna calendar day. */
export function formatViennaCalendarDate(now: Date = new Date()): string {
    const { year, month, day } = getViennaCalendarDate(now);
    return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

/** Compares an ISO UTC timestamp to the current Vienna calendar date. */
export function isSameViennaCalendarDay(isoUtc: string | null | undefined, now: Date = new Date()): boolean {
    if (!isoUtc?.trim()) {
        return false;
    }

    const parsed = new Date(isoUtc);
    if (Number.isNaN(parsed.getTime())) {
        return false;
    }

    const fmt = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Europe/Vienna',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });

    return fmt.format(parsed) === fmt.format(now);
}
