import { describe, expect, it } from 'vitest';

import {
    LICENSE_TEST_MOCK_SCENARIOS,
    licenseTestExpiryFromDays,
    licenseTestScenarioFromDays,
    licenseTestScenarioButtonColor,
    licenseTestScenarioLabelKey,
    licenseTestStatusTagColor,
} from '@/features/license/constants/licenseTestScenarios';

describe('licenseTestScenarios', () => {
    it('defines four canonical mock scenarios', () => {
        expect(LICENSE_TEST_MOCK_SCENARIOS).toHaveLength(4);
        expect(LICENSE_TEST_MOCK_SCENARIOS.map((s) => s.days)).toEqual([30, 7, 1, -1]);
    });

    it('maps status to i18n label keys', () => {
        expect(licenseTestScenarioLabelKey('healthy')).toBe('license.testPanel.mockStatus.healthy');
        expect(licenseTestScenarioLabelKey('expired')).toBe('license.testPanel.mockStatus.expired');
    });

    it('maps scenario colors to ant button colors', () => {
        expect(licenseTestScenarioButtonColor('green')).toBe('green');
        expect(licenseTestScenarioButtonColor('yellow')).toBe('gold');
        expect(licenseTestScenarioButtonColor('red')).toBe('red');
    });

    it('computes expiry ISO from day offset', () => {
        const base = new Date('2026-07-14T12:00:00.000Z');
        expect(licenseTestExpiryFromDays(7, base)).toBe('2026-07-21T12:00:00.000Z');
        expect(licenseTestExpiryFromDays(-1, base)).toBe('2026-07-13T12:00:00.000Z');
    });

    it('maps day offsets to backend scenario presets', () => {
        expect(licenseTestScenarioFromDays(30)).toBe('Days30');
        expect(licenseTestScenarioFromDays(1)).toBe('Days1');
        expect(licenseTestScenarioFromDays(-1)).toBe('Expired');
        expect(licenseTestScenarioFromDays(99)).toBeNull();
    });

    it('maps mock status to tag colors', () => {
        expect(licenseTestStatusTagColor('healthy')).toBe('success');
        expect(licenseTestStatusTagColor('warning')).toBe('warning');
        expect(licenseTestStatusTagColor('urgent')).toBe('orange');
        expect(licenseTestStatusTagColor('expired')).toBe('error');
    });
});
