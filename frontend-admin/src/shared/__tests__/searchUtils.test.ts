import { describe, expect, it } from 'vitest';

import type { GlobalSearchResultItem } from '@/components/admin-layout/GlobalSearch.types';
import {
  buildMenuSearchIndexSource,
  filterGlobalSearchItems,
  filterMenuSearchIndexByRouteKeys,
  splitSearchHighlight,
} from '@/shared/searchUtils';

const sampleItems: GlobalSearchResultItem[] = [
  {
    id: 'a',
    menuKey: '/payments',
    href: '/payments',
    label: 'Zahlungen',
    breadcrumb: 'Verkauf & Vorgänge',
    keywords: ['Zahlungen', '/payments', 'Verkauf & Vorgänge'],
  },
  {
    id: 'b',
    menuKey: '/admin/users',
    href: '/admin/users',
    label: 'Benutzer',
    breadcrumb: 'Verwaltung › Zugriff & Rollen',
    keywords: ['Benutzer', '/admin/users', 'Verwaltung'],
  },
];

describe('filterGlobalSearchItems', () => {
  it('returns empty list for blank query', () => {
    expect(filterGlobalSearchItems(sampleItems, '')).toEqual([]);
  });

  it('matches label contains case-insensitively', () => {
    const results = filterGlobalSearchItems(sampleItems, 'zahl');
    expect(results.map((r) => r.menuKey)).toEqual(['/payments']);
  });

  it('prefers exact label match over contains on path', () => {
    const results = filterGlobalSearchItems(sampleItems, 'Benutzer');
    expect(results[0]?.menuKey).toBe('/admin/users');
  });

  it('matches path segments', () => {
    const results = filterGlobalSearchItems(sampleItems, '/admin/users');
    expect(results[0]?.menuKey).toBe('/admin/users');
  });

  it('matches group names via keywords', () => {
    const results = filterGlobalSearchItems(sampleItems, 'Verwaltung');
    expect(results.map((r) => r.menuKey)).toContain('/admin/users');
  });
});

describe('filterMenuSearchIndexByRouteKeys', () => {
  it('keeps only allowed menu keys', () => {
    const allowed = new Set(['/payments']);
    expect(filterMenuSearchIndexByRouteKeys(sampleItems, allowed).map((i) => i.menuKey)).toEqual([
      '/payments',
    ]);
  });
});

describe('buildMenuSearchIndexSource', () => {
  it('includes group labels for registry-backed items', () => {
    const t = (key: string) => {
      if (key === 'nav.payments') return 'Zahlungen';
      if (key === 'nav.sales') return 'Verkauf';
      if (key === 'nav.operations') return 'Betrieb';
      if (key === 'nav.products') return 'Produkte';
      if (key === 'nav.catalog') return 'Sortiment';
      return key;
    };

    const source = buildMenuSearchIndexSource(t);
    const payments = source.items.find((item) => item.menuKey === '/payments');
    expect(payments?.keywords).toContain('Verkauf');
    expect(source.groupLabels.length).toBeGreaterThan(0);
  });
});

describe('splitSearchHighlight', () => {
  it('marks the matched substring', () => {
    expect(splitSearchHighlight('Zahlungen', 'zahl')).toEqual([
      { text: 'Zahl', highlight: true },
      { text: 'ungen', highlight: false },
    ]);
  });
});
