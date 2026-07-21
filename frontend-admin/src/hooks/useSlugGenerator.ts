'use client';

import { useCallback } from 'react';

import { suggestTenantSlugFromName } from '@/features/super-admin/lib/tenantSlug';

/**
 * Generates a tenant subdomain slug from a company display name.
 */
export function useSlugGenerator() {
  const generateSlug = useCallback((name: string) => suggestTenantSlugFromName(name), []);

  return { generateSlug };
}
