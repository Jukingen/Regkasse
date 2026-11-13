import { describe, expect, it } from '@jest/globals';

import { parseTenantSlugFromPayload } from '../services/customerApp/customerTenantSlug';

describe('parseTenantSlugFromPayload', () => {
  it('parses plain slug', () => {
    expect(parseTenantSlugFromPayload('cafe-demo')).toBe('cafe-demo');
  });

  it('parses regkasse deep link', () => {
    expect(parseTenantSlugFromPayload('regkasse://tenant/cafe-demo')).toBe('cafe-demo');
  });

  it('parses cashregister deep link', () => {
    expect(parseTenantSlugFromPayload('cashregister://tenant/cafe-demo')).toBe('cafe-demo');
  });

  it('parses URL path', () => {
    expect(parseTenantSlugFromPayload('https://sites.example/cafe-demo')).toBe('cafe-demo');
  });

  it('rejects invalid', () => {
    expect(parseTenantSlugFromPayload('!!!')).toBeNull();
  });
});
