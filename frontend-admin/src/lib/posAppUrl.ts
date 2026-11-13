import { isDevelopment } from '@/features/auth/services/devTenant';

const DEFAULT_DEV_POS_URL = 'http://localhost:8081';

/**
 * Optional POS deep link / dev Expo URL. Supports `{slug}` placeholder.
 * Falls back to local Expo dev server in development.
 */
export function buildPosAppOpenUrl(tenantSlug: string | null | undefined): string | null {
  const slug = tenantSlug?.trim().toLowerCase();
  if (!slug || slug === 'admin') {
    return null;
  }

  const template = process.env.NEXT_PUBLIC_POS_APP_URL?.trim();
  if (template) {
    return template.includes('{slug}') ? template.replace(/\{slug\}/gi, slug) : template;
  }

  if (isDevelopment()) {
    return DEFAULT_DEV_POS_URL;
  }

  return null;
}
