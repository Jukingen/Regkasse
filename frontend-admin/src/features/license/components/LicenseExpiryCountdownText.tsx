'use client';

import { useLicenseExpiryCountdown } from '@/features/license/hooks/useLicenseExpiryCountdown';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type LicenseExpiryCountdownTextProps = {
    expiresAt: string | null | undefined;
    labelKey: string;
    t: TranslateFn;
};

export function LicenseExpiryCountdownText({ expiresAt, labelKey, t }: LicenseExpiryCountdownTextProps) {
    const countdown = useLicenseExpiryCountdown(expiresAt);

    if (!countdown) {
        return null;
    }

    return <small>{t(labelKey, { countdown })}</small>;
}
