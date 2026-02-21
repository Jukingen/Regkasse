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
    if (from && !from.isValid()) return 'Invalid start date';
    if (to && !to.isValid()) return 'Invalid end date';
    if (from && to && from.isAfter(to)) return 'Start date must be before or equal to end date';
    return null;
}
