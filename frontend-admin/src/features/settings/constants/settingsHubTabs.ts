/** Super Admin legacy settings hub (`SuperAdminSettings`) tab keys. */
export const SETTINGS_HUB_TAB_KEYS = {
    general: '1',
    localization: '2',
    finanzOnline: '3',
    tse: '4',
    password: '5',
    demo: '6',
} as const;

const SETTINGS_HUB_TAB_SLUG_TO_KEY: Record<string, string> = {
    general: SETTINGS_HUB_TAB_KEYS.general,
    localization: SETTINGS_HUB_TAB_KEYS.localization,
    finanzonline: SETTINGS_HUB_TAB_KEYS.finanzOnline,
    tse: SETTINGS_HUB_TAB_KEYS.tse,
    password: SETTINGS_HUB_TAB_KEYS.password,
    demo: SETTINGS_HUB_TAB_KEYS.demo,
};

/** Maps `?tab=` query slug to Ant Design Tabs `activeKey` (defaults to general). */
export function resolveSettingsHubTabKey(tabSlug: string | null | undefined): string {
    if (!tabSlug?.trim()) {
        return SETTINGS_HUB_TAB_KEYS.general;
    }
    return SETTINGS_HUB_TAB_SLUG_TO_KEY[tabSlug.trim().toLowerCase()] ?? SETTINGS_HUB_TAB_KEYS.general;
}
