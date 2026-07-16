'use client';

import { useEffect, useState } from 'react';

import { formatLicenseExpiryCountdown } from '@/features/license/utils/licenseExpiryCountdown';

const COUNTDOWN_INTERVAL_MS = 60_000;

/** Live license expiry countdown; refreshes every minute. */
export function useLicenseExpiryCountdown(expiresAt: string | null | undefined): string | null {
    const [countdown, setCountdown] = useState<string | null>(() => formatLicenseExpiryCountdown(expiresAt));

    useEffect(() => {
        const tick = () => setCountdown(formatLicenseExpiryCountdown(expiresAt));
        tick();
        const interval = window.setInterval(tick, COUNTDOWN_INTERVAL_MS);
        return () => window.clearInterval(interval);
    }, [expiresAt]);

    return countdown;
}
