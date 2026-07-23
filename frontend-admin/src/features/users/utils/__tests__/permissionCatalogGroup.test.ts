import { describe, expect, it } from 'vitest';

import {
  comparePermissionGroupSlugs,
  PERMISSION_GROUP_ORDER,
  permissionCatalogGroupToSlug,
} from '../permissionCatalogGroup';

describe('permissionCatalogGroupToSlug', () => {
  it('matches backend examples for sidebar-aligned groups', () => {
    expect(permissionCatalogGroupToSlug('Mitarbeiter')).toBe('mitarbeiter');
    expect(permissionCatalogGroupToSlug('Kassenverwaltung')).toBe('kassenverwaltung');
    expect(permissionCatalogGroupToSlug('Sortiment & Preise')).toBe('sortiment_preise');
    expect(permissionCatalogGroupToSlug('Bestellung & Verkauf')).toBe('bestellung_verkauf');
    expect(permissionCatalogGroupToSlug('Kunden & Vorteile')).toBe('kunden_vorteile');
    expect(permissionCatalogGroupToSlug('RKSV & FinanzOnline')).toBe('rksv_finanzonline');
    expect(permissionCatalogGroupToSlug('Tagesabschluss')).toBe('tagesabschluss');
    expect(permissionCatalogGroupToSlug('Backup & Disaster Recovery')).toBe(
      'backup_disaster_recovery'
    );
    expect(permissionCatalogGroupToSlug('Audit & Berichte')).toBe('audit_berichte');
    expect(permissionCatalogGroupToSlug('Digitale Dienste')).toBe('digitale_dienste');
    expect(permissionCatalogGroupToSlug('Sonstige')).toBe('sonstige');
    expect(permissionCatalogGroupToSlug('Other')).toBe('other');
  });

  it('returns other for blank input', () => {
    expect(permissionCatalogGroupToSlug('')).toBe('other');
    expect(permissionCatalogGroupToSlug('   ')).toBe('other');
  });
});

describe('comparePermissionGroupSlugs', () => {
  it('orders by sidebar-aligned PERMISSION_GROUP_ORDER', () => {
    expect(comparePermissionGroupSlugs('mitarbeiter', 'kassenverwaltung', 'M', 'K')).toBeLessThan(0);
    expect(comparePermissionGroupSlugs('tagesabschluss', 'rksv_finanzonline', 'T', 'R')).toBeLessThan(
      0
    );
    expect(
      comparePermissionGroupSlugs('backup_disaster_recovery', 'einstellungen', 'B', 'E')
    ).toBeLessThan(0);
    expect(comparePermissionGroupSlugs('rksv_finanzonline', 'sortiment_preise', 'R', 'S')).toBeLessThan(
      0
    );
    expect(comparePermissionGroupSlugs('einstellungen', 'mitarbeiter', 'E', 'M')).toBeGreaterThan(0);
  });

  it('falls back to label compare for unknown slugs after known ones', () => {
    expect(comparePermissionGroupSlugs('zzz', 'mitarbeiter', 'Z', 'M')).toBeGreaterThan(0);
    expect(comparePermissionGroupSlugs('aaa', 'zzz', 'A', 'Z')).toBeLessThan(0);
  });

  it('lists Mitarbeiter before Kassenverwaltung in the canonical order', () => {
    expect(PERMISSION_GROUP_ORDER.indexOf('mitarbeiter')).toBeLessThan(
      PERMISSION_GROUP_ORDER.indexOf('kassenverwaltung')
    );
  });
});
