import { describe, expect, it } from 'vitest';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

import {
    filterTenantsForHeaderSearch,
    formatTenantDisplay,
    getTenantHeaderDetailLines,
    getTenantHeaderIndicator,
    getTenantHeaderTitle,
    getTenantStatusIcon,
    getTenantSwitcherLicenseBadge,
    partitionTenantsForSwitcher,
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
    if (key === 'license.badge.tenant.none.label') return 'Keine Lizenz';
    if (key === 'license.badge.tenant.none.tooltip') return 'Keine Lizenz.';
    if (key === 'license.badge.tenant.short.expired') return 'Abgelaufen';
    if (key === 'license.badge.tenant.short.days' && params?.days != null) {
        return `${params.days} Tage`;
    }
    if (key === 'license.badge.tenant.short.licensed') return 'Lizenziert';
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

    it('formatTenantDisplay maps seed slugs to hyphen preset slugs', () => {
        expect(formatTenantDisplay(baseRow({ name: 'Test Cafe', slug: 'cafe' }))).toEqual({
            displayName: 'Test Cafe',
            displaySlug: 'test-cafe',
        });
        expect(formatTenantDisplay(baseRow({ name: 'Legacy', slug: 'test_cafe' }))).toEqual({
            displayName: 'Test Cafe',
            displaySlug: 'test-cafe',
        });
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
        expect(filterTenantsForHeaderSearch(rows, 'test-cafe')).toHaveLength(1);
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

    it('partitions development and production tenants by slug', () => {
        const rows = [
            baseRow({ slug: 'dev', name: 'Development' }),
            baseRow({ slug: 'cafe', name: 'Test Cafe' }),
            baseRow({ slug: 'acme', name: 'Acme' }),
        ];
        const { development, production } = partitionTenantsForSwitcher(rows);
        expect(development.map((r) => r.slug)).toEqual(['dev', 'cafe']);
        expect(production.map((r) => r.slug)).toEqual(['acme']);
    });

    it('shows compact license badge for missing license', () => {
        const badge = getTenantSwitcherLicenseBadge(baseRow({ licenseValidUntilUtc: null }), t);
        expect(badge.label).toBe('Keine Lizenz');
        expect(badge.color).toBe('default');
    });

    it('shows short licensed badge when validity is beyond 7 days', () => {
        const until = new Date();
        until.setDate(until.getDate() + 30);
        const badge = getTenantSwitcherLicenseBadge(
            baseRow({ licenseValidUntilUtc: until.toISOString(), licenseKey: 'KEY' }),
            t,
        );
        expect(badge.label).toBe('Lizenziert');
        expect(badge.color).toBe('success');
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
        expect(lines.licenseLine).toBe('Lizenziert');
    });

    it('uses warning days label when seven or fewer days remain', () => {
        const until = new Date();
        until.setDate(until.getDate() + 5);
        const badge = getTenantSwitcherLicenseBadge(
            baseRow({
                licenseValidUntilUtc: until.toISOString(),
                licenseKey: null,
            }),
            t,
        );
        expect(badge.label).toBe('5 Tage');
        expect(badge.color).toBe('warning');
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
