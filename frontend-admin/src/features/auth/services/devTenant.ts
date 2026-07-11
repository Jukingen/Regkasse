import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { canonicalDevTenantSlug } from '@/features/tenancy/devTenantCatalog';

/** Dev-only manual tenant switch (value is tenant slug, not Guid). */
export const DEV_TENANT_LOCAL_STORAGE_KEY = 'dev_tenant_id';

/** Same-tab dev tenant switch (HeaderDevTenantSwitch, impersonation dev path). */
export const DEV_TENANT_CHANGED_EVENT = 'regkasse:dev-tenant-changed';

export type DevTenantChangedDetail = {
    slug: string;
    previousSlug: string | null;
};

export const DEV_TENANT_ENV_KEY = 'NEXT_PUBLIC_DEV_TENANT_ID';

export function isDevelopment(): boolean {
  return process.env.NODE_ENV === 'development';
}

function normalizeSlug(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed) {
    return null;
  }
  return canonicalDevTenantSlug(trimmed);
}

/** Hosts-file dev domains (e.g. dev.regkasse.local). */
export function isLocalDevHostname(host: string): boolean {
  const h = host.trim().toLowerCase();
  return h.endsWith('.regkasse.local') || h.endsWith('.local');
}

/** True when the request host is the platform admin slug (`admin`, `localhost`, loopback). */
export function isPlatformAdminHost(hostname?: string): boolean {
  return getTenantSlugFromSubdomain(hostname) === 'admin';
}

/** Production: first subdomain label when not admin/www/localhost. */
export function getTenantSlugFromSubdomain(hostname?: string): string {
  const host = (hostname ?? (typeof window !== 'undefined' ? window.location.hostname : '')).trim();
  if (
    !host ||
    host === 'localhost' ||
    host.startsWith('127.0.0.1')
  ) {
    return 'admin';
  }

  const parts = host.split('.');
  const first = parts[0]?.toLowerCase() ?? '';
  if (first && first !== 'admin' && first !== 'www') {
    return parts[0]!;
  }

  return 'admin';
}

/**
 * Development: localStorage (per browser profile) → hosts subdomain → env → <c>dev</c>.
 * Use separate browser profiles or *.regkasse.local hosts without sharing localStorage.
 */
export function getDevTenant(): string {
  if (!isDevelopment()) {
    return getTenantSlugFromSubdomain();
  }

  if (typeof window !== 'undefined') {
    const host = window.location.hostname;
    const fromHost = getTenantSlugFromSubdomain(host);
    const stored = normalizeSlug(window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY));

    // Platform host (admin.regkasse.local, localhost): no implicit `dev` mandant.
    if (fromHost === 'admin') {
      return stored ?? 'admin';
    }

    if (stored) return stored;

    if (isLocalDevHostname(host) && fromHost !== 'admin') {
      return fromHost;
    }
  }

  return normalizeSlug(process.env[DEV_TENANT_ENV_KEY]) ?? 'dev';
}

/**
 * Persists dev tenant slug and notifies listeners (same-tab CustomEvent + cross-tab storage).
 * @returns true when the slug actually changed.
 */
export function writeDevTenantSlug(slug: string): boolean {
  if (!isDevelopment() || typeof window === 'undefined') {
    return false;
  }
  const normalized = normalizeSlug(slug);
  if (!normalized) {
    return false;
  }
  const previousSlug = normalizeSlug(window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY));
  if (previousSlug === normalized) {
    return false;
  }
  window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, normalized);
  tenantStorage.persistBootstrap({ tenantSlug: normalized });
  window.dispatchEvent(
    new CustomEvent<DevTenantChangedDetail>(DEV_TENANT_CHANGED_EVENT, {
      detail: { slug: normalized, previousSlug },
    }),
  );
  return true;
}

export function setDevTenant(slug: string): void {
  writeDevTenantSlug(slug);
}

/** Persists dev tenant; reload is handled by {@link useTenantChangeListener} via {@link DEV_TENANT_CHANGED_EVENT}. */
export function setDevTenantAndReload(slug: string): void {
  if (!writeDevTenantSlug(slug) && typeof window !== 'undefined') {
    window.location.reload();
  }
}

export function clearDevTenantOverride(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(DEV_TENANT_LOCAL_STORAGE_KEY);
}

/**
 * Resolves tenant slug for each outgoing API request (never cached on the axios instance).
 * Development: {@link DEV_TENANT_LOCAL_STORAGE_KEY} wins when set (header switcher).
 * Otherwise falls back to {@link getEffectiveTenantSlug} (hosts file, env, production subdomain).
 */
export function resolveTenantSlugForApiRequest(): string {
  if (isDevelopment() && typeof window !== 'undefined') {
    const stored = normalizeSlug(window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY));
    if (stored) {
      return stored;
    }
  }
  return getEffectiveTenantSlug();
}

/**
 * Slug sent as <c>X-Tenant-Id</c> (and dev query) on API requests.
 * Dev: manual override/env. Production: subdomain, then login/bootstrap persistence.
 */
export function getEffectiveTenantSlug(): string {
  const fromHost = getTenantSlugFromSubdomain();

  if (isDevelopment()) {
    return getDevTenant();
  }

  if (fromHost !== 'admin') {
    return fromHost;
  }

  // Super Admin platform host: do not send stale bootstrap slug as X-Tenant-Id.
  const stored = normalizeSlug(
    typeof window !== 'undefined'
      ? window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)
      : null,
  );
  if (stored) {
    return stored;
  }

  return fromHost;
}
