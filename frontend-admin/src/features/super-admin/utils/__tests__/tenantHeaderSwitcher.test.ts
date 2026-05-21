import { describe, expect, it } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

import {
    filterTenantsForHeaderSearch,
    getTenantHeaderDetailLines,
    getTenantHeaderIndicator,
    getTenantHeaderTitle,
    getTenantStatusIcon,
    getTenantSwitcherLicenseBadge,
    sortTenantsForHeaderSwitcher,
    tenantHeaderShowsNoAdminWarning,
} from '../tenantHeaderSwitcher';

const t = (key: string, params?: Record<string, string | number>) => {
    if (key === 'adminShell.tenant.devSwitcher.adminLine' && params?.email) {
        return `Admin: ${params.email}`;
    }
    if (key === 'adminShell.tenant.devSwitcher.noAdmin') return 'Kein Admin zugewiesen';
    if (key === 'adminShell.tenant.devSwitcher.suspendedSuffix') return 'Gesperrt';
    if (key === 'license.badge.tenant.baseTooltip') {
        return 'Mandanten-Lizenz Hinweis.';
    }
    if (key === 'license.badge.tenant.trial.label' && params?.days != null) {
        return `Mandanten-Lizenz: TESTVERSION (${params.days} Tage)`;
    }
    if (key === 'license.badge.tenant.trial.tooltip' && params?.days != null) {
        return `Noch ${params.days} Tage.`;
    }
    if (key === 'license.badge.tenant.expired.label') {
        return 'Mandanten-Lizenz: ABGELAUFEN';
    }
    if (key === 'license.badge.tenant.expired.tooltip') {
        return 'Abgelaufen.';
    }
    return key;
};

const baseRow = (overrides: Partial<AdminTenantListItem>): AdminTenantListItem => ({
    id: '1',
    name: 'Test',
    slug: 'test',
    status: 'active',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
});

describe('tenantHeaderSwitcher', () => {
    it('uses green indicator when active with owner admin', () => {
        expect(
            getTenantStatusIcon({
                status: 'active',
                isActive: true,
                ownerAdminEmail: 'admin@cafe.regkasse.at',
            }),
        ).toBe('🟢');
    });

    it('uses yellow indicator when active without owner admin', () => {
        expect(
            getTenantHeaderIndicator({
                status: 'active',
                isActive: true,
                ownerAdminEmail: null,
            }).kind,
        ).toBe('activeNoAdmin');
    });

    it('uses red indicator when suspended', () => {
        expect(
            getTenantHeaderIndicator({
                status: 'suspended',
                isActive: false,
                ownerAdminEmail: 'admin@dev.regkasse.at',
            }).emoji,
        ).toBe('🔴');
    });

    it('appends suspended suffix to title', () => {
        const title = getTenantHeaderTitle(
            baseRow({ name: 'Development', slug: 'dev', status: 'suspended', isActive: false }),
            t,
        );
        expect(title).toContain('Gesperrt');
        expect(title).toContain('🔴');
    });

    it('sorts active tenants before suspended', () => {
        const sorted = sortTenantsForHeaderSwitcher([
            baseRow({ name: 'Zulu', slug: 'z', status: 'suspended', isActive: false }),
            baseRow({ name: 'Alpha', slug: 'a', status: 'active', isActive: true }),
        ]);
        expect(sorted[0]?.slug).toBe('a');
        expect(sorted[1]?.slug).toBe('z');
    });

    it('filters by slug and admin email', () => {
        const rows = [
            baseRow({ name: 'Cafe', slug: 'cafe', ownerAdminEmail: 'admin@cafe.regkasse.at' }),
            baseRow({ name: 'Bar', slug: 'bar', ownerAdminEmail: null }),
        ];
        expect(filterTenantsForHeaderSearch(rows, 'cafe')).toHaveLength(1);
        expect(filterTenantsForHeaderSearch(rows, 'admin@bar')).toHaveLength(0);
    });

    it('filters by display name case-insensitively', () => {
        const rows = [
            baseRow({ name: 'Café Adler', slug: 'cafe' }),
            baseRow({ name: 'Bar Central', slug: 'bar' }),
        ];
        expect(filterTenantsForHeaderSearch(rows, 'adler')).toHaveLength(1);
        expect(filterTenantsForHeaderSearch(rows, 'BAR')).toHaveLength(1);
    });

    it('builds admin and license detail lines', () => {
        const until = new Date();
        until.setDate(until.getDate() + 21);
        const lines = getTenantHeaderDetailLines(
            baseRow({
                slug: 'cafe',
                ownerAdminEmail: 'admin@cafe.regkasse.at',
                licenseValidUntilUtc: until.toISOString(),
                licenseKey: 'KEY',
            }),
            t,
        );
        expect(lines.adminLine).toBe('Admin: admin@cafe.regkasse.at');
        expect(lines.licenseLine).toBe('Mandanten-Lizenz: TESTVERSION (21 Tage)');
    });

    it('uses Mandanten-Lizenz TESTVERSION label for trial tenants', () => {
        const until = new Date();
        until.setDate(until.getDate() + 8);
        const badge = getTenantSwitcherLicenseBadge(
            baseRow({
                licenseValidUntilUtc: until.toISOString(),
                licenseKey: null,
            }),
            t,
        );
        expect(badge?.label).toMatch(/^Mandanten-Lizenz: TESTVERSION \(\d+ Tage\)$/);
        expect(badge?.color).toBe('blue');
        expect(badge?.tooltip).toContain('Mandanten-Lizenz Hinweis');
    });

    it('omits admin line when no owner admin (pill shown in UI instead)', () => {
        const lines = getTenantHeaderDetailLines(
            baseRow({ slug: 'bar', ownerAdminEmail: null }),
            t,
        );
        expect(lines.adminLine).toBeNull();
        expect(tenantHeaderShowsNoAdminWarning(baseRow({ ownerAdminEmail: null }))).toBe(true);
    });
});
