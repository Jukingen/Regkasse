import { canonicalDevTenantSlug } from '../../constants/devTenantCatalog';
import { secureStorage } from '../secureStorage';
import { TENANT_HTTP_HEADER } from './tenantStorage';

/** Runtime dev override (takes precedence over {@link DEV_TENANT_ENV_KEY}). */
export const DEV_TENANT_LOCAL_STORAGE_KEY = 'dev_tenant_id';

/** @deprecated Use {@link DEV_TENANT_LOCAL_STORAGE_KEY}; read once for migration. */
const LEGACY_DEV_TENANT_SLUG_STORAGE_KEY = 'dev_tenant_slug';

/** @deprecated Alias for {@link DEV_TENANT_LOCAL_STORAGE_KEY}. */
export const DEV_TENANT_SLUG_STORAGE_KEY = DEV_TENANT_LOCAL_STORAGE_KEY;

export const DEV_TENANT_ENV_KEY = 'EXPO_PUBLIC_DEV_TENANT_ID';

const isDev = __DEV__;

function normalizeSlug(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed) {
    return null;
  }
  return canonicalDevTenantSlug(trimmed);
}

/** Slug from Expo env in development; defaults to <c>dev</c> when unset. */
export function getEnvDevTenantSlug(): string | null {
  if (!isDev) return null;
  // Static property access so Expo/Metro can inline EXPO_PUBLIC_* at bundle time.
  return normalizeSlug(process.env.EXPO_PUBLIC_DEV_TENANT_ID) ?? 'dev';
}

export async function getDevTenantSlugOverride(): Promise<string | null> {
  if (!isDev) return null;

  const primary = normalizeSlug(await secureStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY));
  if (primary) return primary;

  const legacy = normalizeSlug(await secureStorage.getItem(LEGACY_DEV_TENANT_SLUG_STORAGE_KEY));
  if (legacy) {
    await secureStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, legacy);
    await secureStorage.removeItem(LEGACY_DEV_TENANT_SLUG_STORAGE_KEY);
    return legacy;
  }

  return null;
}

export async function setDevTenantSlugOverride(slug: string | null): Promise<void> {
  if (!isDev) return;
  const normalized = normalizeSlug(slug);
  if (normalized) {
    await secureStorage.setItem(DEV_TENANT_LOCAL_STORAGE_KEY, normalized);
    await secureStorage.removeItem(LEGACY_DEV_TENANT_SLUG_STORAGE_KEY);
  } else {
    await secureStorage.removeItem(DEV_TENANT_LOCAL_STORAGE_KEY);
    await secureStorage.removeItem(LEGACY_DEV_TENANT_SLUG_STORAGE_KEY);
  }
}

/** Persists slug and updates API base URL query (caller should reload the app). */
export async function setDevTenantAndPersist(slug: string): Promise<void> {
  await setDevTenantSlugOverride(slug);
  const { hydrateDevTenantApiBaseUrl } = await import('../api/config');
  await hydrateDevTenantApiBaseUrl();
}

/** Dev: storage override → env (default <c>dev</c>). Production: null. */
export async function resolveDevTenantSlug(): Promise<string | null> {
  if (!isDev) return null;
  const override = await getDevTenantSlugOverride();
  if (override) return override;
  return getEnvDevTenantSlug();
}

/**
 * Effective tenant slug for API calls.
 * Dev: dev override/env → license/login persistence. Prod: persisted bootstrap only.
 */
export async function resolveEffectiveTenantSlug(
  persistedSlug: string | null | undefined
): Promise<string | null> {
  const devSlug = await resolveDevTenantSlug();
  if (devSlug) return devSlug;
  return normalizeSlug(persistedSlug);
}

/**
 * Appends <c>?tenant=slug</c> to a full request URL (not axios <c>baseURL</c>).
 * Prefer {@link applyTenantHeader} / {@link resolveTenantFetchHeaders} for API calls.
 */
export function appendTenantQueryParam(baseUrl: string, tenantSlug: string): string {
  const slug = normalizeSlug(tenantSlug);
  if (!slug) return baseUrl;

  try {
    const url = new URL(baseUrl);
    url.searchParams.set('tenant', slug);
    return url.toString();
  } catch {
    const sep = baseUrl.includes('?') ? '&' : '?';
    return `${baseUrl}${sep}tenant=${encodeURIComponent(slug)}`;
  }
}

export function applyTenantHeader(
  headers: Record<string, unknown> | undefined,
  tenantSlug: string
): Record<string, unknown> {
  const next = { ...(headers ?? {}) };
  next[TENANT_HTTP_HEADER] = tenantSlug;
  return next;
}
