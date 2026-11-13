import { describe, expect, it } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
  type TenantListItemForSwitcher,
  filterTenantSwitcherItems,
} from '@/features/tenancy/hooks/useTenantListForSwitcher';

const source = (overrides: Partial<AdminTenantListItem>): AdminTenantListItem => ({
  id: overrides.id ?? '1',
  name: overrides.name ?? 'Test',
  slug: overrides.slug ?? 'test',
  status: 'active',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  ...overrides,
});

const item = (overrides: Partial<AdminTenantListItem>): TenantListItemForSwitcher => ({
  id: overrides.id ?? '1',
  name: overrides.name ?? 'Test',
  slug: overrides.slug ?? 'test',
  status: 'active',
  isActive: true,
  adminEmail: null,
  licenseDaysLeft: null,
  statusIcon: '🟡',
  source: source(overrides),
});

describe('filterTenantSwitcherItems', () => {
  const tenants = [
    item({ id: 'a', name: 'Café Adler', slug: 'dev' }),
    item({ id: 'b', name: 'Bar Central', slug: 'prod' }),
    item({ id: 'c', name: 'Market', slug: 'market', status: 'suspended', isActive: false }),
  ];

  it('returns all tenants when query is empty', () => {
    expect(filterTenantSwitcherItems(tenants, '')).toHaveLength(3);
  });

  it('filters by slug', () => {
    expect(filterTenantSwitcherItems(tenants, 'dev')).toHaveLength(1);
    expect(filterTenantSwitcherItems(tenants, 'dev')[0]?.slug).toBe('dev');
  });

  it('filters by name substring', () => {
    expect(filterTenantSwitcherItems(tenants, 'adler')).toHaveLength(1);
  });
});
