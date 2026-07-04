import { describe, expect, it } from 'vitest';
import {
    resolveSettingsHubTabKey,
    SETTINGS_HUB_TAB_KEYS,
} from '@/features/settings/constants/settingsHubTabs';

describe('resolveSettingsHubTabKey', () => {
    it('defaults to general tab when slug is missing or unknown', () => {
        expect(resolveSettingsHubTabKey(null)).toBe(SETTINGS_HUB_TAB_KEYS.general);
        expect(resolveSettingsHubTabKey(undefined)).toBe(SETTINGS_HUB_TAB_KEYS.general);
        expect(resolveSettingsHubTabKey('')).toBe(SETTINGS_HUB_TAB_KEYS.general);
        expect(resolveSettingsHubTabKey('unknown')).toBe(SETTINGS_HUB_TAB_KEYS.general);
    });

    it('maps known slugs case-insensitively', () => {
        expect(resolveSettingsHubTabKey('tse')).toBe(SETTINGS_HUB_TAB_KEYS.tse);
        expect(resolveSettingsHubTabKey('TSE')).toBe(SETTINGS_HUB_TAB_KEYS.tse);
        expect(resolveSettingsHubTabKey('finanzonline')).toBe(SETTINGS_HUB_TAB_KEYS.finanzOnline);
        expect(resolveSettingsHubTabKey('password')).toBe(SETTINGS_HUB_TAB_KEYS.password);
    });
});
