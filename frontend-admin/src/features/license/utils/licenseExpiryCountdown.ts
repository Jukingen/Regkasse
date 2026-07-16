export type LicenseExpiryCountdownParts = {
    days: number;
    hours: number;
    minutes: number;
    totalMs: number;
};

const DAY_MS = 24 * 60 * 60 * 1000;
const HOUR_MS = 60 * 60 * 1000;
const MINUTE_MS = 60 * 1000;

export function getLicenseExpiryCountdownParts(
    expiresAt: string | null | undefined,
    nowMs = Date.now(),
): LicenseExpiryCountdownParts | null {
    if (!expiresAt?.trim()) {
        return null;
    }

    const expiresAtMs = new Date(expiresAt).getTime();
    if (!Number.isFinite(expiresAtMs)) {
        return null;
    }

    const totalMs = expiresAtMs - nowMs;
    if (totalMs <= 0) {
        return { days: 0, hours: 0, minutes: 0, totalMs };
    }

    const days = Math.floor(totalMs / DAY_MS);
    const hours = Math.floor((totalMs % DAY_MS) / HOUR_MS);
    const minutes = Math.floor((totalMs % HOUR_MS) / MINUTE_MS);

    return { days, hours, minutes, totalMs };
}

/** Compact countdown label, e.g. `5d 12h 30m`. Returns null when expiry is unknown or already passed. */
export function formatLicenseExpiryCountdown(
    expiresAt: string | null | undefined,
    nowMs = Date.now(),
): string | null {
    const parts = getLicenseExpiryCountdownParts(expiresAt, nowMs);
    if (!parts || parts.totalMs <= 0) {
        return null;
    }

    return `${parts.days}d ${parts.hours}h ${parts.minutes}m`;
}
