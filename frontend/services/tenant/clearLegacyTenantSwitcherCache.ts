import { storage } from '../../utils/storage';

/** Pre-switcher selection key from older POS builds. */
const LEGACY_SELECTED_TENANT_KEY = 'selectedTenant';

/** Drops obsolete local keys (keeps {@link TENANT_STORAGE_KEYS.switcherList} for offline fallback). */
export async function clearLegacyTenantSwitcherCache(): Promise<void> {
  await storage.removeItem(LEGACY_SELECTED_TENANT_KEY);
}
