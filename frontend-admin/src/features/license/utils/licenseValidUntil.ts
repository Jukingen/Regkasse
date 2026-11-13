/**
 * Canonical mandant license expiry helpers.
 *
 * Source of truth: API `validUntil` / `validUntilUtc` (ISO UTC).
 * Days remaining match backend `ComputeActivationDaysRemaining` before the unsigned clamp:
 * `Math.ceil((validUntil - now) / 1 day)`.
 * Display uses the UTC calendar date so `23:59:59Z` does not shift to the next local day.
 */

const DAY_MS = 24 * 60 * 60 * 1000;

export const LICENSE_VALID_UNTIL_EMPTY = '—' as const;

export type LicenseValidUntilFormat = 'date' | 'datetime' | 'auto';

export function parseLicenseValidUntilMs(
  validUntil: string | Date | null | undefined
): number | null {
  if (validUntil == null) return null;
  if (validUntil instanceof Date) {
    const ms = validUntil.getTime();
    return Number.isFinite(ms) ? ms : null;
  }

  const trimmed = String(validUntil).trim();
  if (!trimmed) return null;
  const ms = Date.parse(trimmed);
  return Number.isFinite(ms) ? ms : null;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

function utcParts(ms: number): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
  millisecond: number;
} {
  const date = new Date(ms);
  return {
    year: date.getUTCFullYear(),
    month: date.getUTCMonth() + 1,
    day: date.getUTCDate(),
    hour: date.getUTCHours(),
    minute: date.getUTCMinutes(),
    second: date.getUTCSeconds(),
    millisecond: date.getUTCMilliseconds(),
  };
}

export function licenseValidUntilHasTime(
  validUntil: string | Date | null | undefined
): boolean {
  const ms = parseLicenseValidUntilMs(validUntil);
  if (ms == null) return false;
  const parts = utcParts(ms);
  return parts.hour !== 0 || parts.minute !== 0 || parts.second !== 0 || parts.millisecond !== 0;
}

/**
 * Signed whole days until expiry from `validUntil`.
 * Future: ceil (1 hour left → 1). Past: ceil of a negative span (just expired → 0).
 * Invalid / missing → `null` so callers can fall back to API `daysRemaining`.
 */
export function calculateLicenseDaysRemaining(
  validUntil: string | Date | null | undefined,
  nowMs = Date.now()
): number | null {
  const untilMs = parseLicenseValidUntilMs(validUntil);
  if (untilMs == null) return null;
  return Math.ceil((untilMs - nowMs) / DAY_MS);
}

/** Active-license cache patch: never negative. */
export function calculateLicenseDaysRemainingUnsigned(
  validUntil: string | Date | null | undefined,
  nowMs = Date.now()
): number {
  const days = calculateLicenseDaysRemaining(validUntil, nowMs);
  return days == null ? 0 : Math.max(0, days);
}

/**
 * UTC `DD.MM.YYYY`, with `HH:mm` when `format` is `datetime` or `auto` and the stamp is not midnight UTC.
 */
export function formatLicenseValidUntil(
  validUntil: string | Date | null | undefined,
  format: LicenseValidUntilFormat = 'auto'
): string {
  const ms = parseLicenseValidUntilMs(validUntil);
  if (ms == null) return LICENSE_VALID_UNTIL_EMPTY;

  const parts = utcParts(ms);
  const date = `${pad2(parts.day)}.${pad2(parts.month)}.${parts.year}`;
  const showTime =
    format === 'datetime' ||
    (format === 'auto' &&
      (parts.hour !== 0 || parts.minute !== 0 || parts.second !== 0 || parts.millisecond !== 0));

  if (!showTime) return date;
  return `${date} ${pad2(parts.hour)}:${pad2(parts.minute)}`;
}
