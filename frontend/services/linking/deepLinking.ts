/**
 * Deep-link resolve/create helpers on top of expo-linking.
 * App schemes: cashregister (native) + regkasse (QR / email brand links).
 */
import * as Linking from 'expo-linking';
import type { QueryParams } from 'expo-linking';

import { normalizeCustomerTenantSlug } from '../customerApp/customerTenantSlug';

export const APP_LINK_SCHEMES = ['cashregister', 'regkasse'] as const;

export type DeepLinkIntent =
  | { type: 'customerTenant'; slug: string }
  | { type: 'customerHome'; slug?: string }
  | {
      type: 'orderTracker';
      tenant?: string;
      orderNumber?: string;
      phone?: string;
    }
  | { type: 'login' }
  | { type: 'unhandled'; path: string | null; scheme: string | null };

function firstQueryParam(params: QueryParams | null | undefined, keys: string[]): string | null {
  if (!params) return null;
  for (const key of keys) {
    const value = params[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (Array.isArray(value) && value[0]?.trim()) return value[0].trim();
  }
  return null;
}

/** hostname + path segments (lowercased), e.g. regkasse://tenant/cafe → ['tenant','cafe']. */
export function deepLinkPathSegments(url: string): string[] {
  const parsed = Linking.parse(url);
  const parts: string[] = [];
  if (parsed.hostname) parts.push(parsed.hostname);
  if (parsed.path) {
    parts.push(...parsed.path.split('/').filter(Boolean));
  }
  return parts.map((p) => p.toLowerCase());
}

/**
 * Map an inbound URL (email / push / QR) to a navigation intent.
 * Safe for POS + customer surfaces; callers decide which intents to honor.
 */
export function resolveDeepLink(url: string | null | undefined): DeepLinkIntent | null {
  if (!url?.trim()) return null;

  let parsed: ReturnType<typeof Linking.parse>;
  try {
    parsed = Linking.parse(url.trim());
  } catch {
    return null;
  }

  const segments = deepLinkPathSegments(url);
  const querySlug = normalizeCustomerTenantSlug(
    firstQueryParam(parsed.queryParams, ['tenant', 'slug', 'tenantSlug'])
  );

  if (segments[0] === 'tenant' && segments[1]) {
    const slug = normalizeCustomerTenantSlug(segments[1]);
    if (slug) return { type: 'customerTenant', slug };
  }

  if (segments[0] === 'customer') {
    return { type: 'customerHome', slug: querySlug ?? undefined };
  }

  if (segments[0] === 'order-tracker' || segments[0] === 'orders') {
    return {
      type: 'orderTracker',
      tenant: querySlug ?? undefined,
      orderNumber:
        firstQueryParam(parsed.queryParams, ['order', 'orderNumber', 'orderNo']) ?? undefined,
      phone: firstQueryParam(parsed.queryParams, ['phone', 'tel']) ?? undefined,
    };
  }

  if (segments[0] === 'login' || (segments[0] === '(auth)' && segments[1] === 'login')) {
    return { type: 'login' };
  }

  // Scheme-only / empty path with tenant query → customer home
  if (querySlug && segments.length === 0) {
    return { type: 'customerHome', slug: querySlug };
  }

  return {
    type: 'unhandled',
    path: segments.join('/') || parsed.path,
    scheme: parsed.scheme,
  };
}

/** Build a production-style deep link (prefers cashregister scheme). */
export function createAppDeepLink(path: string, queryParams?: QueryParams): string {
  return Linking.createURL(path.replace(/^\//, ''), {
    scheme: 'cashregister',
    queryParams,
  });
}

export function createTenantDeepLink(slug: string): string {
  const normalized = normalizeCustomerTenantSlug(slug);
  if (!normalized) {
    return createAppDeepLink('customer');
  }
  return createAppDeepLink(`tenant/${normalized}`);
}

export function createOrderTrackerDeepLink(input: {
  tenant: string;
  orderNumber: string;
  phone?: string;
}): string {
  return createAppDeepLink('order-tracker', {
    tenant: input.tenant,
    order: input.orderNumber,
    ...(input.phone ? { phone: input.phone } : {}),
  });
}
