import { describe, expect, it } from 'vitest';

import { buildTenantSelectorLabel, getTenantSelectorStatus } from '../tenantSelectorLabel';

const t = (key: string, params?: Record<string, string>) => {
    if (key === 'superadmin.selector.adminAssigned' && params?.email) {
        return `Admin: ${params.email}`;
    }
    if (key === 'superadmin.selector.noAdmin') return 'Kein Admin zugewiesen';
    if (key === 'superadmin.selector.demoTenant') return 'Demo-Mandant';
    return key;
};

describe('tenantSelectorLabel', () => {
    it('shows no admin when owner missing and not demo preset', () => {
        const status = getTenantSelectorStatus(
            { ownerAdminEmail: null, isDemoPreset: false },
            t,
        );
        expect(status.kind).toBe('noAdmin');
        expect(status.suffix).toContain('Kein Admin zugewiesen');
    });

    it('shows admin email when owner assigned', () => {
        const label = buildTenantSelectorLabel(
            {
                name: 'Test Bar',
                slug: 'bar',
                ownerAdminEmail: 'admin@bar.regkasse.at',
                isDemoPreset: false,
            },
            t,
        );
        expect(label).toBe('Test Bar (bar) - ✅ Admin: admin@bar.regkasse.at');
    });

    it('prefers demo preset over owner for dev slug', () => {
        const label = buildTenantSelectorLabel(
            {
                name: 'Development',
                slug: 'dev',
                ownerAdminEmail: 'admin@dev.regkasse.at',
                isDemoPreset: true,
            },
            t,
        );
        expect(label).toContain('Demo-Mandant');
        expect(label).not.toContain('admin@dev');
    });
});
