import {
  DEV_TENANT_LOCAL_STORAGE_KEY,
  getDevTenant,
  getEffectiveTenantSlug,
  getTenantSlugFromSubdomain,
  isDevelopment,
} from '@/features/auth/services/devTenant';
import { tenantStorage } from '@/features/auth/services/tenantStorage';

export { DEV_TENANT_LOCAL_STORAGE_KEY };

/** Active tenant slug for API headers: dev localStorage / subdomain / persisted bootstrap. */
export function resolveTenantSlug(): string {
  return getEffectiveTenantSlug();
}

/** First host label when not admin/www/localhost. */
export function resolveTenantSlugFromHost(hostname?: string): string {
  return getTenantSlugFromSubdomain(hostname);
}

/** Dev-only `localStorage` override, if set. */
export function resolveDevTenantOverride(): string | null {
  if (!isDevelopment() || typeof window === 'undefined') {
    return null;
  }
  const stored = window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)?.trim();
  return stored || null;
}

/** Dev slug source chain (localStorage → *.regkasse.local host → env → `dev`). */
export function resolveDevTenantSlug(): string {
  return getDevTenant();
}

export function resolveTenantId(): string | null {
  return tenantStorage.getTenantId();
}
