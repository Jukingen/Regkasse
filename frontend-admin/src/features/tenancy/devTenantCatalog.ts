import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

/**
 * Dev/demo tenant presets — aligned with POS {@link frontend/constants/devTenantCatalog.ts}.
 * `presetSlug` is shown in switchers; `canonicalSlug` is the DB seed slug (API resolution).
 */
export const DEV_TENANT_CATALOG = [
  { canonicalSlug: 'dev', displayName: 'Development', presetSlug: 'dev' },
  { canonicalSlug: 'prod', displayName: 'Production', presetSlug: 'prod' },
] as const;

const SLUG_ORDER = new Map(
  DEV_TENANT_CATALOG.map((entry, index) => [entry.canonicalSlug, index] as const),
);

const PRESET_SLUG_BY_CANONICAL = new Map(
  DEV_TENANT_CATALOG.map((entry) => [entry.canonicalSlug, entry.presetSlug] as const),
);

/** Legacy / UI slugs → DB seed slugs (see backend DevTenantSlugAliases). */
const SLUG_ALIASES: Record<string, string> = {
  test_cafe: 'dev',
  'test-cafe': 'dev',
  cafe: 'dev',
  test_bar: 'prod',
  'test-bar': 'prod',
  bar: 'prod',
};

export function canonicalDevTenantSlug(slug: string): string {
  const trimmed = slug.trim();
  if (!trimmed) {
    return trimmed;
  }
  const lower = trimmed.toLowerCase();
  return SLUG_ALIASES[lower] ?? trimmed;
}

export function getDevTenantCatalogDisplayName(slug: string): string | null {
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  const entry = DEV_TENANT_CATALOG.find((row) => row.canonicalSlug === canonical);
  return entry?.displayName ?? null;
}

/** Hyphen slug for switcher subtitle (e.g. prod → prod). */
export function formatDisplaySlug(slug: string): string {
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  const preset = PRESET_SLUG_BY_CANONICAL.get(
    canonical as (typeof DEV_TENANT_CATALOG)[number]['canonicalSlug'],
  );
  if (preset) {
    return preset;
  }
  return slug.trim().replace(/_/g, '-');
}

/** POS/FA shared row labels for header tenant switcher. */
export function formatTenantDisplay(
  tenant: Pick<AdminTenantListItem, 'name' | 'slug'>,
): { displayName: string; displaySlug: string } {
  return {
    displayName: getDevTenantCatalogDisplayName(tenant.slug) ?? tenant.name,
    displaySlug: formatDisplaySlug(tenant.slug),
  };
}

/** Lower index = higher in dev switcher lists; unknown tenants sort after catalog entries. */
export function devTenantCatalogSortIndex(slug: string): number {
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  return SLUG_ORDER.get(canonical as (typeof DEV_TENANT_CATALOG)[number]['canonicalSlug']) ?? 999;
}

/** Dev switcher: Entwicklung / Test section (dev, demo seeds, or slug contains "test"). */
export function isDevelopmentOrTestTenantSlug(slug: string): boolean {
  const raw = slug.trim().toLowerCase();
  if (!raw) {
    return false;
  }
  if (raw === 'dev' || raw.includes('test')) {
    return true;
  }
  const display = formatDisplaySlug(slug).toLowerCase();
  if (display.includes('test')) {
    return true;
  }
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  return canonical === 'dev' || canonical === 'prod';
}
