/**
 * Dev/demo tenant presets — aligned with FA devTenantCatalog (display + preset slugs).
 * API calls resolve preset slugs (e.g. test-cafe) to DB slugs (cafe) via canonicalDevTenantSlug.
 */
export const DEV_TENANT_PRESETS = [
  { name: 'Default', slug: 'default' },
  { name: 'Development', slug: 'dev' },
  { name: 'Test Cafe', slug: 'test-cafe' },
  { name: 'Test Bar', slug: 'test-bar' },
  { name: 'Test Tenant', slug: 'test' },
] as const;

export type DevTenantPresetSlug = (typeof DEV_TENANT_PRESETS)[number]['slug'];

const SLUG_ALIASES: Record<string, string> = {
  test_cafe: 'cafe',
  'test-cafe': 'cafe',
  cafe: 'cafe',
  test_bar: 'bar',
  'test-bar': 'bar',
  bar: 'bar',
  dev: 'dev',
  default: 'default',
  test: 'test',
};

export function canonicalDevTenantSlug(slug: string): string {
  const trimmed = slug.trim();
  if (!trimmed) {
    return trimmed;
  }
  const lower = trimmed.toLowerCase();
  return SLUG_ALIASES[lower] ?? trimmed;
}

const DISPLAY_NAME_BY_CANONICAL = new Map<string, string>(
  DEV_TENANT_PRESETS.map((row) => [canonicalDevTenantSlug(row.slug), row.name]),
);

export function getDevTenantPresetName(slug: string): string | null {
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  return DISPLAY_NAME_BY_CANONICAL.get(canonical) ?? null;
}

/** @deprecated Use getDevTenantPresetName */
export const getDevTenantPresetLabel = getDevTenantPresetName;

export function formatDisplaySlug(slug: string): string {
  const canonical = canonicalDevTenantSlug(slug).toLowerCase();
  const preset = DEV_TENANT_PRESETS.find(
    (row) => canonicalDevTenantSlug(row.slug).toLowerCase() === canonical,
  );
  return preset?.slug ?? slug.trim().replace(/_/g, '-');
}

export function isSameDevTenantPreset(slugA: string, slugB: string): boolean {
  return canonicalDevTenantSlug(slugA).toLowerCase() === canonicalDevTenantSlug(slugB).toLowerCase();
}
