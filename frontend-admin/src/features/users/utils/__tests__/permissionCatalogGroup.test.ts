import { describe, expect, it } from 'vitest';

import { permissionCatalogGroupToSlug } from '../permissionCatalogGroup';

describe('permissionCatalogGroupToSlug', () => {
  it('matches backend examples for matrix groups', () => {
    expect(permissionCatalogGroupToSlug('User & Role')).toBe('user_role');
    expect(permissionCatalogGroupToSlug('Cash & Shift')).toBe('cash_shift');
    expect(permissionCatalogGroupToSlug('Audit & Report')).toBe('audit_report');
    expect(permissionCatalogGroupToSlug('Order & Sale')).toBe('order_sale');
    expect(permissionCatalogGroupToSlug('FinanzOnline')).toBe('finanzonline');
    expect(permissionCatalogGroupToSlug('Sonstige')).toBe('sonstige');
    expect(permissionCatalogGroupToSlug('Other')).toBe('other');
  });

  it('returns other for blank input', () => {
    expect(permissionCatalogGroupToSlug('')).toBe('other');
    expect(permissionCatalogGroupToSlug('   ')).toBe('other');
  });
});
