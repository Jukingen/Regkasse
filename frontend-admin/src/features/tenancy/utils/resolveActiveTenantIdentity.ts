import { canonicalDevTenantSlug } from '@/features/tenancy/devTenantCatalog';

export function tenantSlugsMatch(
  left: string | null | undefined,
  right: string | null | undefined
): boolean {
  if (!left?.trim() || !right?.trim()) {
    return false;
  }
  return canonicalDevTenantSlug(left).toLowerCase() === canonicalDevTenantSlug(right).toLowerCase();
}

/**
 * Mandant id for API calls: prefer switcher row; JWT id only when slug matches active context.
 * Avoids Super Admin JWT (legacy `default`) overriding dev header / switcher slug.
 */
export function resolveActiveTenantId(input: {
  resolvedRowId?: string | null;
  jwtTenantId?: string | null;
  jwtTenantSlug?: string | null;
  activeTenantSlug?: string | null;
}): string | null {
  if (input.resolvedRowId?.trim()) {
    return input.resolvedRowId.trim();
  }
  if (input.jwtTenantId?.trim() && tenantSlugsMatch(input.jwtTenantSlug, input.activeTenantSlug)) {
    return input.jwtTenantId.trim();
  }
  return null;
}
