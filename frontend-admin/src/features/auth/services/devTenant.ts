import { tenantStorage } from '@/features/auth/services/tenantStorage';

/** Dev-only manual tenant switch (value is tenant slug, not Guid). */
export const DEV_TENANT_LOCAL_STORAGE_KEY = 'dev_tenant_id';

export const DEV_TENANT_ENV_KEY = 'NEXT_PUBLIC_DEV_TENANT_ID';

function isDevelopment(): boolean {
  return process.env.NODE_ENV === 'development';
}

function normalizeSlug(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : null;
}

/** Hosts-file dev domains (e.g. cafe.regkasse.local). */
export function isLocalDevHostname(host: string): boolean {
  const h = host.trim().toLowerCase();
  return h.endsWith('.regkasse.local') || h.endsWith('.local');
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
    const stored = normalizeSlug(window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY));
    if (stored) return stored;

    const host = window.location.hostname;
    if (isLocalDevHostname(host)) {
      const fromHost = getTenantSlugFromSubdomain(host);
      if (fromHost !== 'admin') return fromHost;
    }
  }

  return normalizeSlug(process.env[DEV_TENANT_ENV_KEY]) ?? 'dev';
}

export function setDevTenant(slug: string): void {
  if (!isDevelopment() || typeof window === 'undefined') return;
  const normalized = normalizeSlug(slug);
  if (!normalized) return;
  window.localStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, normalized);
  tenantStorage.persistBootstrap({ tenantSlug: normalized });
}

/** Persists dev tenant and reloads the page (dev switcher). */
export function setDevTenantAndReload(slug: string): void {
  setDevTenant(slug);
  if (typeof window !== 'undefined') {
    window.location.reload();
  }
}

export function clearDevTenantOverride(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(DEV_TENANT_LOCAL_STORAGE_KEY);
}

/**
 * Slug sent as <c>X-Tenant-Id</c> (and dev query) on API requests.
 * Dev: manual override/env. Production: subdomain, then login/bootstrap persistence.
 */
export function getEffectiveTenantSlug(): string {
  if (isDevelopment()) {
    return getDevTenant();
  }

  const fromHost = getTenantSlugFromSubdomain();
  if (fromHost !== 'admin') {
    return fromHost;
  }

  return tenantStorage.getTenantSlug() ?? fromHost;
}
