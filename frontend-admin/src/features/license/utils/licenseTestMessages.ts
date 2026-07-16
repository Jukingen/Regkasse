'use client';

import type { LicenseTestScenarioPreset } from '@/features/license/constants/licenseTestScenarios';
import { formatGermanDateTime } from '@/lib/dateFormatter';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export function licenseTestScenarioDisplayDays(
    scenario: LicenseTestScenarioPreset,
): number | 'expired' {
    switch (scenario) {
        case 'Days1':
            return 1;
        case 'Days7':
            return 7;
        case 'Days30':
            return 30;
        case 'Expired':
            return 'expired';
        default:
            return 1;
    }
}

export function getLicenseTestScenarioSuccessMessage(
    scenario: LicenseTestScenarioPreset,
    t: TranslateFn,
): string {
    const days = licenseTestScenarioDisplayDays(scenario);
    if (days === 'expired') {
        return t('license.testPanel.scenarioExpiredSuccess');
    }
    return t('license.testPanel.scenarioSuccess', { days });
}

export function getLicenseTestManualSuccessMessage(validUntilIso: string, t: TranslateFn): string {
    const formatted = formatGermanDateTime(validUntilIso);
    return t('license.testPanel.manualSuccess', {
        date: formatted === '—' ? validUntilIso : formatted,
    });
}
