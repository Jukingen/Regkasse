import { storage } from '../../utils/storage';
import {
  normalizeCustomerTenantSlug,
  parseTenantSlugFromPayload,
} from './customerTenantSlug';

export { parseTenantSlugFromPayload } from './customerTenantSlug';

/** Isolated from POS cashier tenant bootstrap (`tenant_slug`). */
const CUSTOMER_TENANT_SLUG_KEY = 'customer_app_tenant_slug';

/**
 * Resolve tenant slug for the shared customer mobile app.
 * Order: explicit storage → QR/deep-link payload → env default (dev only).
 */
export async function getTenantSlug(qrOrLink?: string | null): Promise<string | null> {
  const fromQr = parseTenantSlugFromPayload(qrOrLink);
  if (fromQr) return fromQr;

  const stored = normalizeCustomerTenantSlug(await storage.getItem(CUSTOMER_TENANT_SLUG_KEY));
  if (stored) return stored;

  if (__DEV__) {
    const env = normalizeCustomerTenantSlug(process.env.EXPO_PUBLIC_DEV_TENANT_ID);
    if (env) return env;
  }

  return null;
}

export async function setCustomerTenantSlug(slug: string | null): Promise<void> {
  const normalized = normalizeCustomerTenantSlug(slug);
  if (normalized) {
    await storage.setItem(CUSTOMER_TENANT_SLUG_KEY, normalized);
  } else {
    await storage.removeItem(CUSTOMER_TENANT_SLUG_KEY);
  }
}

export async function clearCustomerTenantSlug(): Promise<void> {
  await storage.removeItem(CUSTOMER_TENANT_SLUG_KEY);
}
