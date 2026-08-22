import { describe, expect, it } from 'vitest';

import { isOperationalPickerTenant } from '@/features/tenancy/hooks/useTenantList';

describe('isOperationalPickerTenant', () => {
  it('keeps active business tenants', () => {
    expect(isOperationalPickerTenant({ isActive: true, slug: 'dev' })).toBe(true);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'prod' })).toBe(true);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'adler' })).toBe(true);
  });

  it('hides platform, Test Cafe, and Test Bar leftovers', () => {
    expect(isOperationalPickerTenant({ isActive: true, slug: 'platform' })).toBe(false);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'admin' })).toBe(false);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'cafe' })).toBe(false);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'bar' })).toBe(false);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'test-cafe' })).toBe(false);
    expect(isOperationalPickerTenant({ isActive: true, slug: 'test-bar' })).toBe(false);
  });

  it('hides inactive tenants', () => {
    expect(isOperationalPickerTenant({ isActive: false, slug: 'dev' })).toBe(false);
  });
});
