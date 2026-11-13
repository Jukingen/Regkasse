import { describe, expect, it } from 'vitest';

import { ADMIN_OVERVIEW_HREF, buildAdminBreadcrumbs } from '@/shared/adminShellLabels';

describe('buildAdminBreadcrumbs', () => {
  const t = (key: string) => (key === 'common.breadcrumb.overview' ? 'Overview' : key);

  it('prepends overview by default', () => {
    expect(buildAdminBreadcrumbs(t, [{ title: 'Customers' }])).toEqual([
      { title: 'Overview', href: ADMIN_OVERVIEW_HREF },
      { title: 'Customers' },
    ]);
  });

  it('can omit overview', () => {
    expect(buildAdminBreadcrumbs(t, [{ title: 'Only' }], { includeOverview: false })).toEqual([
      { title: 'Only' },
    ]);
  });
});
