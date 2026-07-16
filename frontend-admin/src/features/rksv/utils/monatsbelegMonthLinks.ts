/**
 * Deep-link and date-range helpers for interactive Monatsbeleg timeline month cards.
 */

/** First/last calendar day of a Vienna month as YYYY-MM-DD (date-only filters). */
export function monthDateRangeIso(year: number, month: number): { from: string; to: string } {
    const mm = String(month).padStart(2, '0');
    const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
    return {
        from: `${year}-${mm}-01`,
        to: `${year}-${mm}-${String(lastDay).padStart(2, '0')}`,
    };
}

export function buildMonatsbelegMonthDeepLink(params: {
    origin: string;
    registerId?: string;
    year: number;
    month: number;
}): string {
    const qs = new URLSearchParams();
    qs.set('focus', 'monatsbeleg');
    qs.set('year', String(params.year));
    qs.set('month', String(params.month));
    if (params.registerId?.trim()) {
        qs.set('registerId', params.registerId.trim());
    }
    return `${params.origin}/rksv/sonderbelege?${qs.toString()}`;
}

export function buildMonthReceiptsHref(cashRegisterId: string, year: number, month: number): string {
    const { from, to } = monthDateRangeIso(year, month);
    const qs = new URLSearchParams({
        cashRegisterId,
        issuedFrom: from,
        issuedTo: to,
    });
    return `/receipts?${qs.toString()}`;
}

export function buildMonthPaymentsHref(cashRegisterId: string, year: number, month: number): string {
    const { from, to } = monthDateRangeIso(year, month);
    const qs = new URLSearchParams({
        cashRegisterId,
        startDate: from,
        endDate: to,
    });
    return `/payments?${qs.toString()}`;
}
