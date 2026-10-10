/**
 * @jest-environment node
 *
 * Deep-link parsing for email / push / QR without booting Expo Router.
 */
import { describe, expect, it, jest } from '@jest/globals';

import { parseTenantSlugFromPayload } from '../services/customerApp/customerTenantSlug';
import {
  createAppDeepLink,
  createOrderTrackerDeepLink,
  createTenantDeepLink,
  deepLinkPathSegments,
  resolveDeepLink,
} from '../services/linking/deepLinking';
import { resolvePosTenantDeepLinkTarget } from '../services/linking/posTenantDeepLink';

jest.mock('expo-linking', () => {
  // Lightweight parse compatible with expo-linking's ParsedURL shape.
  return {
    parse: (url: string) => {
      const parsed = new URL(url);
      const queryParams: Record<string, string> = {};
      parsed.searchParams.forEach((value, key) => {
        queryParams[key] = value;
      });
      return {
        scheme: parsed.protocol.replace(/:$/, ''),
        hostname: parsed.hostname || null,
        path: parsed.pathname.replace(/^\//, '') || null,
        queryParams,
      };
    },
    createURL: (path: string, opts?: { scheme?: string; queryParams?: Record<string, string> }) => {
      const scheme = opts?.scheme ?? 'cashregister';
      const qs = opts?.queryParams ? `?${new URLSearchParams(opts.queryParams).toString()}` : '';
      return `${scheme}://${path.replace(/^\//, '')}${qs}`;
    },
  };
});

describe('resolveDeepLink', () => {
  it('maps regkasse://tenant/{slug} (email / QR brand link)', () => {
    expect(resolveDeepLink('regkasse://tenant/cafe-demo')).toEqual({
      type: 'customerTenant',
      slug: 'cafe-demo',
    });
  });

  it('maps cashregister://tenant/{slug} (app scheme)', () => {
    expect(resolveDeepLink('cashregister://tenant/cafe-demo')).toEqual({
      type: 'customerTenant',
      slug: 'cafe-demo',
    });
  });

  it('maps customer home with tenant query', () => {
    expect(resolveDeepLink('cashregister://customer?tenant=cafe-demo')).toEqual({
      type: 'customerHome',
      slug: 'cafe-demo',
    });
  });

  it('maps order-tracker deep link from push/email', () => {
    expect(
      resolveDeepLink('cashregister://order-tracker?tenant=cafe-demo&order=AB-12&phone=%2B43123')
    ).toEqual({
      type: 'orderTracker',
      tenant: 'cafe-demo',
      orderNumber: 'AB-12',
      phone: '+43123',
    });
  });

  it('maps login deep link', () => {
    expect(resolveDeepLink('cashregister://login')).toEqual({ type: 'login' });
  });

  it('maps online payment gateway callback', () => {
    expect(
      resolveDeepLink('cashregister://online-payment/callback?onlinePaymentId=op-1&status=completed')
    ).toEqual({
      type: 'onlinePaymentCallback',
      onlinePaymentId: 'op-1',
      gatewayStatus: 'completed',
    });
  });

  it('maps payment-result deep links and web return path', () => {
    expect(
      resolveDeepLink('regkasse://payment-result?redirect_status=succeeded&payment_intent=pi_1')
    ).toEqual({
      type: 'onlinePaymentCallback',
      onlinePaymentId: 'pi_1',
      gatewayStatus: 'succeeded',
    });
    expect(
      resolveDeepLink('https://pos.regkasse.at/payment/result?redirect_status=failed')
    ).toEqual({
      type: 'onlinePaymentCallback',
      onlinePaymentId: undefined,
      gatewayStatus: 'failed',
    });
  });

  it('returns null for empty', () => {
    expect(resolveDeepLink(null)).toBeNull();
    expect(resolveDeepLink('')).toBeNull();
  });
});

describe('resolvePosTenantDeepLinkTarget', () => {
  it('resolves regkasse://tenant/{slug} to login with that tenant when signed out', () => {
    expect(
      resolvePosTenantDeepLinkTarget({
        url: 'regkasse://tenant/praxis-nord',
        isAuthenticated: false,
        customerSurface: false,
      })
    ).toEqual({ kind: 'login', slug: 'praxis-nord' });
  });

  it('resolves cashregister://tenant/{slug} to the same tenant', () => {
    expect(
      resolvePosTenantDeepLinkTarget({
        url: 'cashregister://tenant/Salon-Mitte',
        isAuthenticated: false,
        customerSurface: false,
      })
    ).toEqual({ kind: 'login', slug: 'salon-mitte' });
  });

  it('keeps an authenticated POS session on the register for that tenant link', () => {
    expect(
      resolvePosTenantDeepLinkTarget({
        url: 'regkasse://tenant/taxi-wien',
        isAuthenticated: true,
        customerSurface: false,
      })
    ).toEqual({ kind: 'pos', slug: 'taxi-wien' });
  });

  it('keeps the customer surface on /customer for the same scheme', () => {
    expect(
      resolvePosTenantDeepLinkTarget({
        url: 'regkasse://tenant/cafe-demo',
        isAuthenticated: false,
        customerSurface: true,
      })
    ).toEqual({ kind: 'customer', slug: 'cafe-demo' });
  });

  it('ignores links that are not a tenant route', () => {
    expect(
      resolvePosTenantDeepLinkTarget({
        url: 'regkasse://login',
        isAuthenticated: false,
        customerSurface: false,
      })
    ).toBeNull();
  });
});

describe('deepLinkPathSegments', () => {
  it('splits hostname + path', () => {
    expect(deepLinkPathSegments('regkasse://tenant/cafe-demo')).toEqual(['tenant', 'cafe-demo']);
  });
});

describe('create*DeepLink helpers', () => {
  it('builds tenant and order-tracker URLs', () => {
    expect(createTenantDeepLink('Cafe-Demo')).toBe('cashregister://tenant/cafe-demo');
    expect(createOrderTrackerDeepLink({ tenant: 'cafe-demo', orderNumber: 'X1' })).toBe(
      'cashregister://order-tracker?tenant=cafe-demo&order=X1'
    );
    expect(createAppDeepLink('customer')).toBe('cashregister://customer');
  });
});

describe('parseTenantSlugFromPayload (scheme alignment)', () => {
  it('parses both app schemes', () => {
    expect(parseTenantSlugFromPayload('regkasse://tenant/cafe-demo')).toBe('cafe-demo');
    expect(parseTenantSlugFromPayload('cashregister://tenant/cafe-demo')).toBe('cafe-demo');
    expect(parseTenantSlugFromPayload('cashregister://customer?tenant=cafe-demo')).toBe(
      'cafe-demo'
    );
  });
});
