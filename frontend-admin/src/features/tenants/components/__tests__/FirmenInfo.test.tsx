import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { FirmenInfo } from '../FirmenInfo';

const mockUseTenant = vi.fn();

vi.mock('@/i18n', () => ({
    useI18n: () => ({
        t: (key: string) => {
            const labels: Record<string, string> = {
                'common.tenant.tenant': 'Mandant',
                'common.tenant.tenantAlt': 'Firma',
                'common.tenant.companyInfo': 'Firmen-Info',
                'common.dataList.errorLoadTitle': 'Fehler beim Laden',
                'common.dataList.loadingTip': 'Laden',
                'adminShell.tenant.infoCardName': 'Anzeigename',
                'adminShell.tenant.infoCardSlug': 'Slug',
                'adminShell.tenant.infoCardId': 'Firmen-ID',
                'adminShell.tenant.info.license': 'Lizenzstatus',
                'adminShell.tenant.selectTenantFirstTitle': 'Kein Mandant',
                'adminShell.tenant.selectTenantFirstBody': 'Bitte Mandant wählen',
                'license.phase.labels.active': 'Aktiv',
                'license.phase.labels.noLicense': 'Keine Lizenz',
                'license.mandant.validUntil': 'Gültig bis',
            };
            return labels[key] ?? key;
        },
        formatLocale: 'de-DE',
    }),
    formatDate: (value: string) => value.slice(0, 10),
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
    useTenant: () => mockUseTenant(),
}));

describe('FirmenInfo', () => {
    it('renders tenant fields from useTenant', () => {
        mockUseTenant.mockReturnValue({
            tenant: {
                id: 'dev-tenant-id',
                name: 'Development',
                slug: 'dev',
                licenseValid: true,
                licenseValidUntilUtc: '2026-04-25T00:00:00Z',
            },
            isLoading: false,
            error: null,
            setTenant: vi.fn(),
            refresh: vi.fn(),
        });

        render(<FirmenInfo />);

        expect(screen.getByText('Firmen-Info')).toBeTruthy();
        expect(screen.getAllByText('Development').length).toBeGreaterThan(0);
        expect(screen.getByText('dev')).toBeTruthy();
        expect(screen.getByText('dev-tenant-id')).toBeTruthy();
        expect(screen.getByText('2026-04-25')).toBeTruthy();
        expect(screen.getByText('Aktiv')).toBeTruthy();
    });

    it('shows warning when tenant is missing', () => {
        mockUseTenant.mockReturnValue({
            tenant: null,
            isLoading: false,
            error: null,
            setTenant: vi.fn(),
            refresh: vi.fn(),
        });

        render(<FirmenInfo />);
        expect(screen.getByText('Kein Mandant')).toBeTruthy();
    });

    it('shows loading spinner when useTenant is loading', () => {
        mockUseTenant.mockReturnValue({
            tenant: null,
            isLoading: true,
            error: null,
            setTenant: vi.fn(),
            refresh: vi.fn(),
        });

        render(<FirmenInfo />);
        expect(screen.getByLabelText('Laden')).toBeTruthy();
    });
});
