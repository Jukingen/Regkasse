import type { MissingMonth, MonatsbelegRegisterStatusItemDto } from '@/api/generated/model';
import {
    formatViennaYearMonth,
    getMonthDifference,
    getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';

export type MissingMonatsbelegComplianceItem = {
    year: number;
    month: number;
    monthName: string;
    daysLate: number;
    isOverdue: boolean;
};

const germanMonthFormatter = new Intl.DateTimeFormat('de-DE', {
    month: 'long',
    timeZone: 'Europe/Vienna',
});

export function formatMonatsbelegMonthNameDe(month1to12: number): string {
    return germanMonthFormatter.format(new Date(Date.UTC(2026, month1to12 - 1, 1)));
}

/** Whole Vienna calendar days past the API legal deadline (0 when still on time). */
export function computeDaysLateFromDeadline(deadline: string | null | undefined, now: Date = new Date()): number {
    if (!deadline?.trim()) return 0;

    const viennaDateFmt = new Intl.DateTimeFormat('en-CA', {
        timeZone: 'Europe/Vienna',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });

    const todayAnchor = new Date(`${viennaDateFmt.format(now)}T00:00:00Z`);
    const deadlineAnchor = new Date(`${deadline.trim().slice(0, 10)}T00:00:00Z`);
    const diffDays = Math.floor((todayAnchor.getTime() - deadlineAnchor.getTime()) / 86_400_000);
    return diffDays > 0 ? diffDays : 0;
}

/** Tenant-wide missing Monatsbeleg periods (deduped by year-month). */
export function aggregateMissingMonatsbelegeForCompliance(
    overview: MonatsbelegRegisterStatusItemDto[] | undefined,
    now: Date = new Date(),
): MissingMonatsbelegComplianceItem[] {
    const byKey = new Map<string, MissingMonatsbelegComplianceItem>();

    for (const item of overview ?? []) {
        for (const missing of item.status?.missingMonths ?? []) {
            const key = `${missing.year}-${String(missing.month).padStart(2, '0')}`;
            const daysLate = computeDaysLateFromDeadline(missing.deadline, now);
            const candidate: MissingMonatsbelegComplianceItem = {
                year: missing.year,
                month: missing.month,
                monthName: formatMonatsbelegMonthNameDe(missing.month),
                daysLate,
                isOverdue: missing.isOverdue,
            };
            const existing = byKey.get(key);
            if (!existing || candidate.daysLate > existing.daysLate || candidate.isOverdue) {
                byKey.set(key, candidate);
            }
        }
    }

    return Array.from(byKey.values()).sort((a, b) => {
        const anchorA = a.year * 12 + (a.month - 1);
        const anchorB = b.year * 12 + (b.month - 1);
        return anchorA - anchorB;
    });
}

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
