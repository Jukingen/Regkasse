import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { FirmenInfoTenant } from '../FirmenInfo';
import { FirmenInfo } from '../FirmenInfo';

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string, params?: Record<string, string | number>) => {
            if (key === 'common.tenant.tenant') return 'Mandant';
            if (key === 'common.tenant.tenantAlt') return 'Firma';
            if (key === 'common.tenant.companyInfo') return 'Firmen-Info';
            if (key === 'adminShell.tenant.infoCardName') return 'Anzeigename';
            if (key === 'adminShell.tenant.infoCardSlug') return 'Slug';
            if (key === 'adminShell.tenant.infoCardId') return 'Firmen-ID';
            if (key === 'adminShell.tenant.info.license') return 'Lizenzstatus';
            if (key === 'adminShell.tenant.info.registeredAt') return 'Registriert am';
            if (key === 'license.phase.labels.active') return 'Aktiv';
            return params ? `${key}:${JSON.stringify(params)}` : key;
        },
        formatLocale: 'de-DE',
    }),
    formatDate: (value: string) => value.slice(0, 10),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
    useCurrentTenant: () => ({
        tenantId: null,
        tenantSlug: null,
        isRealTenantSlug: false,
        isTenantRecordLoading: false,
    }),
}));

vi.mock('@/features/tenant/hooks/useTenantInfo', () => ({
    useTenantInfo: () => ({
        tenantSlug: null,
        tenantName: null,
        registeredAt: null,
        licenseStatus: {
            kind: 'no_license',
            daysRemaining: 0,
            daysExpired: 0,
            canWrite: false,
            canManageUsers: false,
            canAccess: false,
        },
        isLoading: false,
        isTenantRecordLoading: false,
    }),
}));

const sampleTenant: FirmenInfoTenant = {
    id: 'dev-tenant-id',
    name: 'Development',
    slug: 'dev',
    createdAt: '2026-01-15T10:00:00Z',
    licenseStatus: {
        kind: 'active',
        daysRemaining: 30,
        daysExpired: 0,
        canWrite: true,
        canManageUsers: true,
        canAccess: true,
    },
};

describe('FirmenInfo', () => {
    it('renders tenant fields from the provided tenant prop', () => {
        render(<FirmenInfo tenant={sampleTenant} />);

        expect(screen.getByText('Firmen-Info')).toBeTruthy();
        expect(screen.getAllByText('Development').length).toBeGreaterThan(0);
        expect(screen.getByText('dev')).toBeTruthy();
        expect(screen.getByText('dev-tenant-id')).toBeTruthy();
        expect(screen.getByText('2026-01-15')).toBeTruthy();
        expect(screen.getByText('Aktiv')).toBeTruthy();
    });

    it('returns null when tenant prop is explicitly null', () => {
        const { container } = render(<FirmenInfo tenant={null} />);
        expect(container.innerHTML).toBe('');
    });
});
