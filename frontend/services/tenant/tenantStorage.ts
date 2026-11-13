import { storage } from '../../utils/storage';
import { secureStorage } from '../secureStorage';
import type { TenantSwitcherListItem } from './tenantSwitcherApi';
import { sessionManager } from '../session/sessionManager';

/** Matches backend <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> (value is tenant slug). */
export const TENANT_HTTP_HEADER = 'X-Tenant-Id';

export const TENANT_STORAGE_KEYS = {
  tenantId: 'tenant_id',
  tenantSlug: 'tenant_slug',
  apiBaseUrl: 'api_base_url',
  licenseBootstrap: 'license_bootstrap',
  /** Last successful GET /api/tenants/switcher payload (offline fallback for dev switcher). */
  switcherList: 'tenants',
} as const;

export type TenantBootstrap = {
  tenantId?: string | null;
  tenantSlug?: string | null;
  apiBaseUrl?: string | null;
};

function normalizeGuid(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : null;
}

function normalizeSlug(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : null;
}

function normalizeApiBaseUrl(value: string | null | undefined): string | null {
  const trimmed = value?.trim().replace(/\/+$/, '');
  return trimmed && trimmed.length > 0 ? trimmed : null;
}

/** Sensitive tenant bootstrap lives in SecureStore; switcher list stays in AsyncStorage. */
export const tenantStorage = {
  async getTenantId(): Promise<string | null> {
    return normalizeGuid(await secureStorage.getItem(TENANT_STORAGE_KEYS.tenantId));
  },

  async getTenantSlug(): Promise<string | null> {
    return normalizeSlug(await secureStorage.getItem(TENANT_STORAGE_KEYS.tenantSlug));
  },

  async getApiBaseUrl(): Promise<string | null> {
    return normalizeApiBaseUrl(await secureStorage.getItem(TENANT_STORAGE_KEYS.apiBaseUrl));
  },

  async persistBootstrap(input: TenantBootstrap): Promise<void> {
    const tenantId = normalizeGuid(input.tenantId);
    const tenantSlug = normalizeSlug(input.tenantSlug);
    const apiBaseUrl = normalizeApiBaseUrl(input.apiBaseUrl);

    if (tenantId) {
      await secureStorage.setItem(TENANT_STORAGE_KEYS.tenantId, tenantId);
    }
    if (tenantSlug) {
      await secureStorage.setItem(TENANT_STORAGE_KEYS.tenantSlug, tenantSlug);
    }
    if (apiBaseUrl) {
      await secureStorage.setItem(TENANT_STORAGE_KEYS.apiBaseUrl, apiBaseUrl);
    }
  },

  async clear(): Promise<void> {
    await secureStorage.multiRemove([
      TENANT_STORAGE_KEYS.tenantId,
      TENANT_STORAGE_KEYS.tenantSlug,
      TENANT_STORAGE_KEYS.apiBaseUrl,
      TENANT_STORAGE_KEYS.licenseBootstrap,
    ]);
  },

  async getCachedSwitcherList(): Promise<TenantSwitcherListItem[]> {
    const raw = await storage.getItem(TENANT_STORAGE_KEYS.switcherList);
    if (!raw) return [];

    try {
      const parsed: unknown = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];

      return parsed
        .filter(
          (row): row is TenantSwitcherListItem =>
            row != null &&
            typeof row === 'object' &&
            typeof (row as TenantSwitcherListItem).id === 'string' &&
            typeof (row as TenantSwitcherListItem).name === 'string' &&
            typeof (row as TenantSwitcherListItem).slug === 'string'
        )
        .map((row) => ({
          id: row.id,
          name: row.name,
          slug: row.slug,
          status: typeof row.status === 'string' ? row.status : 'active',
          isActive: row.isActive !== false,
        }));
    } catch {
      return [];
    }
  },

  async setCachedSwitcherList(items: TenantSwitcherListItem[]): Promise<void> {
    await storage.setItem(TENANT_STORAGE_KEYS.switcherList, JSON.stringify(items));
  },

  async clearSwitcherListCache(): Promise<void> {
    await storage.removeItem(TENANT_STORAGE_KEYS.switcherList);
  },
};

export async function getCurrentTenantSlug(): Promise<string | null> {
  return await tenantStorage.getTenantSlug();
}

export type FetchFreshTenantsResult = {
  tenants: TenantSwitcherListItem[];
  fromCache: boolean;
};

/**
 * Always requests GET /api/tenants/switcher; updates switcher cache on success.
 * On failure returns last cached list (may be empty).
 */
async function resolveSwitcherListFromCache(): Promise<FetchFreshTenantsResult> {
  const tenants = await tenantStorage.getCachedSwitcherList();
  return { tenants, fromCache: tenants.length > 0 };
}

export async function fetchFreshTenants(): Promise<FetchFreshTenantsResult> {
  const token = await sessionManager.getAccessToken();
  if (!token || sessionManager.isExpired(token)) {
    return await resolveSwitcherListFromCache();
  }

  const { fetchTenantSwitcherList } = await import('./tenantSwitcherApi');

  try {
    const tenants = await fetchTenantSwitcherList(false);
    await tenantStorage.setCachedSwitcherList(tenants);
    return { tenants, fromCache: false };
  } catch {
    return await resolveSwitcherListFromCache();
  }
}

/** @deprecated Alias for {@link fetchFreshTenants}. */
export async function getTenants(): Promise<TenantSwitcherListItem[]> {
  const { tenants } = await fetchFreshTenants();
  return tenants;
}
