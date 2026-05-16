import { storage } from '../../utils/storage';

/** Matches backend <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> (value is tenant slug). */
export const TENANT_HTTP_HEADER = 'X-Tenant-Id';

export const TENANT_STORAGE_KEYS = {
  tenantId: 'tenant_id',
  tenantSlug: 'tenant_slug',
  apiBaseUrl: 'api_base_url',
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

export const tenantStorage = {
  async getTenantId(): Promise<string | null> {
    return normalizeGuid(await storage.getItem(TENANT_STORAGE_KEYS.tenantId));
  },

  async getTenantSlug(): Promise<string | null> {
    return normalizeSlug(await storage.getItem(TENANT_STORAGE_KEYS.tenantSlug));
  },

  async getApiBaseUrl(): Promise<string | null> {
    return normalizeApiBaseUrl(await storage.getItem(TENANT_STORAGE_KEYS.apiBaseUrl));
  },

  async persistBootstrap(input: TenantBootstrap): Promise<void> {
    const tenantId = normalizeGuid(input.tenantId);
    const tenantSlug = normalizeSlug(input.tenantSlug);
    const apiBaseUrl = normalizeApiBaseUrl(input.apiBaseUrl);

    if (tenantId) {
      await storage.setItem(TENANT_STORAGE_KEYS.tenantId, tenantId);
    }
    if (tenantSlug) {
      await storage.setItem(TENANT_STORAGE_KEYS.tenantSlug, tenantSlug);
    }
    if (apiBaseUrl) {
      await storage.setItem(TENANT_STORAGE_KEYS.apiBaseUrl, apiBaseUrl);
    }
  },

  async clear(): Promise<void> {
    await storage.multiRemove([
      TENANT_STORAGE_KEYS.tenantId,
      TENANT_STORAGE_KEYS.tenantSlug,
      TENANT_STORAGE_KEYS.apiBaseUrl,
    ]);
  },
};
