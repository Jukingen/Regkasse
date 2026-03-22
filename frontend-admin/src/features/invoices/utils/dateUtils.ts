import dayjs from 'dayjs';

/**
 * Date boundary normalization for invoice date-range filtering.
 * 
 * Convention:
 * - `from` is INCLUSIVE: start of selected day (00:00:00.000)
 * - `to` is INCLUSIVE: end of selected day via exclusive-next-day on backend (< to+1day)
 *   Frontend sends start-of-day for `to`; backend adds +1 day with `<` operator.
 * - All dates sent as ISO 8601 UTC strings.
 */

/** Normalize "from" date to start-of-day ISO string */
export function normalizeFromDate(d: dayjs.Dayjs): string {
    return d.startOf('day').toISOString();
}

/** Normalize "to" date to start-of-day ISO string (backend handles +1 day) */
export function normalizeToDate(d: dayjs.Dayjs): string {
    return d.startOf('day').toISOString();
}

/** Validate date range, returns null if valid or error message */
export function validateDateRange(
    from: dayjs.Dayjs | null | undefined,
    to: dayjs.Dayjs | null | undefined
): string | null {
    if (from && !from.isValid()) return 'Ungültiges Startdatum';
    if (to && !to.isValid()) return 'Ungültiges Enddatum';
    if (from && to && from.isAfter(to)) return 'Startdatum muss vor oder am Enddatum liegen';
    return null;
}
