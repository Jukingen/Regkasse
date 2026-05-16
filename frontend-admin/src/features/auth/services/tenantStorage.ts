/** Matches backend <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> (value is tenant slug). */
export const TENANT_HTTP_HEADER = 'X-Tenant-Id';

const TENANT_ID_KEY = 'rk_admin_tenant_id';
const TENANT_SLUG_KEY = 'rk_admin_tenant_slug';
const API_BASE_URL_KEY = 'rk_admin_api_base_url';

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
  getTenantId(): string | null {
    if (typeof window === 'undefined') return null;
    return normalizeGuid(window.localStorage.getItem(TENANT_ID_KEY));
  },

  getTenantSlug(): string | null {
    if (typeof window === 'undefined') return null;
    return normalizeSlug(window.localStorage.getItem(TENANT_SLUG_KEY));
  },

  getApiBaseUrl(): string | null {
    if (typeof window === 'undefined') return null;
    return normalizeApiBaseUrl(window.localStorage.getItem(API_BASE_URL_KEY));
  },

  persistBootstrap(input: TenantBootstrap): void {
    if (typeof window === 'undefined') return;

    const tenantId = normalizeGuid(input.tenantId);
    const tenantSlug = normalizeSlug(input.tenantSlug);
    const apiBaseUrl = normalizeApiBaseUrl(input.apiBaseUrl);

    if (tenantId) {
      window.localStorage.setItem(TENANT_ID_KEY, tenantId);
    }
    if (tenantSlug) {
      window.localStorage.setItem(TENANT_SLUG_KEY, tenantSlug);
    }
    if (apiBaseUrl) {
      window.localStorage.setItem(API_BASE_URL_KEY, apiBaseUrl);
    }
  },

  clear(): void {
    if (typeof window === 'undefined') return;
    window.localStorage.removeItem(TENANT_ID_KEY);
    window.localStorage.removeItem(TENANT_SLUG_KEY);
    window.localStorage.removeItem(API_BASE_URL_KEY);
  },
};
