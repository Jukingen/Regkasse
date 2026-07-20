import { describe, expect, it } from 'vitest';

import { ADMIN_OVERVIEW_HREF } from '@/shared/adminShellLabels';
import { buildPathBreadcrumbs } from '@/shared/buildPathBreadcrumbs';

describe('buildPathBreadcrumbs', () => {
    const t = (key: string) => {
        const map: Record<string, string> = {
            'common.breadcrumb.overview': 'Overview',
            'common.breadcrumb.admin': 'Administration',
            'common.breadcrumb.detail': 'Details',
            'nav.settingsHub': 'Settings',
            'nav.companySettings': 'Company',
            'settings.companyPage.pageTitle': 'Company',
        };
        return map[key] ?? key;
    };

    it('returns overview only for dashboard', () => {
        expect(buildPathBreadcrumbs('/dashboard', t)).toEqual([{ title: 'Overview' }]);
        expect(buildPathBreadcrumbs('/', t)).toEqual([{ title: 'Overview' }]);
    });

    it('resolves catalog leaves for nested settings', () => {
        const crumbs = buildPathBreadcrumbs('/settings/company', t);
        expect(crumbs[0]).toEqual({ title: 'Overview', href: ADMIN_OVERVIEW_HREF });
        expect(crumbs[crumbs.length - 1].href).toBeUndefined();
        expect(crumbs.some((c) => c.title === 'Settings')).toBe(true);
    });

    it('masks UUID segments', () => {
        const id = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
        const crumbs = buildPathBreadcrumbs(`/tenant/${id}/orders`, t);
        expect(crumbs.some((c) => c.title === 'Details')).toBe(true);
    });
});
