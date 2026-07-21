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

  it('returns null for empty', () => {
    expect(resolveDeepLink(null)).toBeNull();
    expect(resolveDeepLink('')).toBeNull();
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
