const HOUR_MS = 60 * 60 * 1000;

/** Wall-clock hours until expiry (ceil). Null when expiry is missing/invalid. */
export function getLicenseHoursRemaining(
  expiresAt: string | null | undefined,
  nowMs = Date.now(),
): number | null {
  if (!expiresAt?.trim()) {
    return null;
  }

  const expiresAtMs = new Date(expiresAt).getTime();
  if (!Number.isFinite(expiresAtMs)) {
    return null;
  }

  const remainingMs = expiresAtMs - nowMs;
  if (remainingMs <= 0) {
    return 0;
  }

  return Math.ceil(remainingMs / HOUR_MS);
}

/** Normalize API day counts without calendar truncation side effects. */
export function normalizeLicenseDaysRemaining(value: number | null | undefined): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    return 0;
  }
  return Math.trunc(value);
}

export type LicenseRemainingPreference =
  | { kind: 'hours'; hours: number }
  | { kind: 'days'; days: number };

/**
 * Prefer hours when less than one day remains so POS matches FA header/page copy.
 */
export function preferLicenseHoursRemaining(
  daysRemaining: number,
  expiresAt: string | null | undefined,
  nowMs = Date.now(),
): LicenseRemainingPreference | null {
  const hours = getLicenseHoursRemaining(expiresAt, nowMs);
  if (hours !== null && hours > 0 && hours < 24 && daysRemaining >= 0) {
    return { kind: 'hours', hours };
  }

  if (daysRemaining > 0) {
    return { kind: 'days', days: daysRemaining };
  }

  return null;
}

/** German operator-facing remaining fragment: "5 Std." / "3 Tage" / "1 Tag". */
export function formatLicenseRemainingDe(
  daysRemaining: number,
  expiresAt: string | null | undefined,
  nowMs = Date.now(),
): string | null {
  const pref = preferLicenseHoursRemaining(daysRemaining, expiresAt, nowMs);
  if (!pref) return null;
  if (pref.kind === 'hours') {
    return `${pref.hours} Std.`;
  }
  return `${pref.days} Tag${pref.days === 1 ? '' : 'e'}`;
}
