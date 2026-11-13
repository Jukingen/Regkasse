/**
 * Pure slug helpers for the shared customer mobile app (no React Native imports).
 */

function normalizeSlug(value: string | null | undefined): string | null {
  const trimmed = value?.trim().toLowerCase();
  if (!trimmed) return null;
  if (!/^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$/.test(trimmed)) return null;
  return trimmed;
}

export { normalizeSlug as normalizeCustomerTenantSlug };

/**
 * Accepts plain slug, path URL, or deep links:
 * - `regkasse://tenant/{slug}` / `cashregister://tenant/{slug}`
 * - `tenant:{slug}`
 * - `…/customer?tenant={slug}` (query)
 */
export function parseTenantSlugFromPayload(payload?: string | null): string | null {
  if (!payload?.trim()) return null;
  const raw = payload.trim();

  const tenantScheme = /^(?:regkasse|cashregister):\/\/tenant\/([a-z0-9-]+)/i.exec(raw);
  if (tenantScheme?.[1]) return normalizeSlug(tenantScheme[1]);

  const tenantPrefix = /^tenant:([a-z0-9-]+)$/i.exec(raw);
  if (tenantPrefix?.[1]) return normalizeSlug(tenantPrefix[1]);

  try {
    const url = new URL(raw);
    // hostname form: scheme://tenant/{slug}
    if (url.hostname.toLowerCase() === 'tenant') {
      const fromHost = normalizeSlug(url.pathname.split('/').filter(Boolean)[0]);
      if (fromHost) return fromHost;
    }
    const parts = url.pathname.split('/').filter(Boolean);
    const tenantIdx = parts.findIndex((p) => p.toLowerCase() === 'tenant');
    if (tenantIdx >= 0 && parts[tenantIdx + 1]) {
      const fromSegment = normalizeSlug(parts[tenantIdx + 1]);
      if (fromSegment) return fromSegment;
    }
    const last = parts[parts.length - 1];
    const fromPath = normalizeSlug(last);
    if (fromPath && last?.toLowerCase() !== 'customer' && last?.toLowerCase() !== 'tenant') {
      return fromPath;
    }
    const q = url.searchParams.get('tenant') ?? url.searchParams.get('slug');
    return normalizeSlug(q);
  } catch {
    return normalizeSlug(raw);
  }
}
