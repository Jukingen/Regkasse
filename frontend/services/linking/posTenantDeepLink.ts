/**
 * POS handling for regkasse://tenant/{slug} and cashregister://tenant/{slug}.
 * The customer app surface keeps the existing /customer bridge.
 */
import { resolveDeepLink } from './deepLinking';

export type PosTenantDeepLinkTarget =
  | { kind: 'customer'; slug: string }
  | { kind: 'login'; slug: string }
  | { kind: 'pos'; slug: string };

export function isCustomerAppSurface(): boolean {
  return (process.env.EXPO_PUBLIC_APP_SURFACE ?? '').trim().toLowerCase() === 'customer';
}

/**
 * `regkasse://tenant/{slug}` on the POS app opens login with that tenant when
 * the cashier is signed out, and the register when a session already exists.
 */
export function resolvePosTenantDeepLinkTarget(input: {
  url: string;
  isAuthenticated: boolean;
  customerSurface?: boolean;
}): PosTenantDeepLinkTarget | null {
  const intent = resolveDeepLink(input.url);
  if (!intent || intent.type !== 'customerTenant') return null;

  const customerSurface = input.customerSurface ?? isCustomerAppSurface();
  if (customerSurface) return { kind: 'customer', slug: intent.slug };
  if (!input.isAuthenticated) return { kind: 'login', slug: intent.slug };
  return { kind: 'pos', slug: intent.slug };
}
