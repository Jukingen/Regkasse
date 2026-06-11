import type { MissingMonth, MonatsbelegRegisterStatusItemDto } from '@/api/generated/model';
import {
    formatViennaYearMonth,
    getMonthDifference,
    getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';

export type PastMissingMonatsbelegEntry = {
    cashRegisterId: string;
    year: number;
    month: number;
    yearMonth: string;
    isOverdue: boolean;
};

export function isCurrentViennaMonth(year: number, month: number, now: Date = new Date()): boolean {
    const current = getViennaCalendarYearMonth(now);
    return year === current.year && month === current.month;
}

export function isPastViennaMonth(year: number, month: number, now: Date = new Date()): boolean {
    return getMonthDifference(year, month, now) > 0;
}

/** Missing months before the current Vienna calendar month. */
export function filterPastMissingMonths(
    missingMonths: MissingMonth[] | null | undefined,
    now: Date = new Date(),
): MissingMonth[] {
    return (missingMonths ?? []).filter((entry) => isPastViennaMonth(entry.year, entry.month, now));
}

export function collectPastMissingMonatsbelege(
    overview: MonatsbelegRegisterStatusItemDto[] | undefined,
    now: Date = new Date(),
): PastMissingMonatsbelegEntry[] {
    const entries: PastMissingMonatsbelegEntry[] = [];
    for (const item of overview ?? []) {
        const cashRegisterId = item.cashRegisterId?.trim();
        if (!cashRegisterId) continue;
        for (const missing of filterPastMissingMonths(item.status?.missingMonths, now)) {
            entries.push({
                cashRegisterId,
                year: missing.year,
                month: missing.month,
                yearMonth: formatViennaYearMonth(missing.year, missing.month),
                isOverdue: missing.isOverdue,
            });
        }
    }
    entries.sort((a, b) => {
        const anchorA = a.year * 12 + (a.month - 1);
        const anchorB = b.year * 12 + (b.month - 1);
        if (anchorA !== anchorB) return anchorA - anchorB;
        return a.cashRegisterId.localeCompare(b.cashRegisterId);
    });
    return entries;
}

export function countPastMissingMonatsbelege(
    overview: MonatsbelegRegisterStatusItemDto[] | undefined,
    now: Date = new Date(),
): number {
    return collectPastMissingMonatsbelege(overview, now).length;
}
