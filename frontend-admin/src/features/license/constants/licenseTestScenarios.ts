/** Simulated license expiry scenarios for the dev-only test panel. */
export type LicenseTestMockStatus = 'healthy' | 'warning' | 'urgent' | 'expired';

export type LicenseTestScenarioColor = 'green' | 'yellow' | 'orange' | 'red';

export type LicenseTestMockScenario = {
    days: number;
    color: LicenseTestScenarioColor;
    status: LicenseTestMockStatus;
};

/** Canonical mock scenarios — keep in sync with QA documentation. */
export const LICENSE_TEST_MOCK_SCENARIOS: readonly LicenseTestMockScenario[] = [
    { days: 30, color: 'green', status: 'healthy' },
    { days: 7, color: 'yellow', status: 'warning' },
    { days: 1, color: 'orange', status: 'urgent' },
    { days: -1, color: 'red', status: 'expired' },
] as const;

const SCENARIO_LABEL_KEYS: Record<LicenseTestMockStatus, string> = {
    healthy: 'license.testPanel.mockStatus.healthy',
    warning: 'license.testPanel.mockStatus.warning',
    urgent: 'license.testPanel.mockStatus.urgent',
    expired: 'license.testPanel.mockStatus.expired',
};

/** i18n key for a scenario button label. */
export function licenseTestScenarioLabelKey(status: LicenseTestMockStatus): string {
    return SCENARIO_LABEL_KEYS[status];
}

/** Ant Design button `color` token for a mock scenario. */
export function licenseTestScenarioButtonColor(
    color: LicenseTestScenarioColor,
): 'green' | 'gold' | 'orange' | 'red' {
    if (color === 'yellow') return 'gold';
    return color;
}

/** Compute ISO expiry from today + scenario offset days. */
export function licenseTestExpiryFromDays(days: number, from: Date = new Date()): string {
    const date = new Date(from);
    date.setDate(date.getDate() + days);
    return date.toISOString();
}

/** Backend <c>LicenseTestScenario</c> preset names. */
export type LicenseTestScenarioPreset = 'Days1' | 'Days7' | 'Days30' | 'Expired';

/** Map mock scenario offset to backend preset enum. */
export function licenseTestScenarioFromDays(days: number): LicenseTestScenarioPreset | null {
    switch (days) {
        case 30:
            return 'Days30';
        case 7:
            return 'Days7';
        case 1:
            return 'Days1';
        case -1:
            return 'Expired';
        default:
            return null;
    }
}

/** Map mock status to Ant Design tag color for the live snapshot card. */
export function licenseTestStatusTagColor(status: LicenseTestMockStatus | string): string {
    switch (status) {
        case 'healthy':
        case 'active':
            return 'success';
        case 'warning':
        case 'expiring_soon':
            return 'warning';
        case 'urgent':
            return 'orange';
        case 'expired':
            return 'error';
        default:
            return 'default';
    }
}
