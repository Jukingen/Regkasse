import { resolveTenantSlugForApiRequest } from '@/features/auth/services/devTenant';

/** TanStack Query scope segment so caches do not bleed across mandants after switch without reload. */
export function tenantQueryScope(): string {
  if (typeof window === 'undefined') {
    return 'ssr';
  }
  return resolveTenantSlugForApiRequest();
}

export function withTenantScope<const T extends readonly unknown[]>(
  key: T
): readonly [...T, string] {
  return [...key, tenantQueryScope()] as const;
}
