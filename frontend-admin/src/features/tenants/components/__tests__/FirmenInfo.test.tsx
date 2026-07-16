import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { FirmenInfo } from '../FirmenInfo';

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
                'license.phase.labels.active': 'Aktiv',
                'license.phase.labels.noLicense': 'Keine Lizenz',
                'license.mandant.validUntil': 'Gültig bis',
            };
            return labels[key] ?? key;
        },
        formatLocale: 'de-DE',
    }),
    formatGermanDateTime: (value: string) => {
        const d = new Date(value);
        const dd = String(d.getDate()).padStart(2, '0');
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const yyyy = d.getFullYear();
        const hh = String(d.getHours()).padStart(2, '0');
        const mi = String(d.getMinutes()).padStart(2, '0');
        return `${dd}.${mm}.${yyyy} ${hh}:${mi}`;
    },
}));

const devTenant = {
    id: 'dev-tenant-id',
    name: 'Development',
    slug: 'dev',
    licenseValid: true,
    licenseValidUntilUtc: '2026-04-25T00:00:00Z',
};

describe('FirmenInfo', () => {
    it('renders tenant fields from tenant prop', () => {
        render(<FirmenInfo tenant={devTenant} />);

        expect(screen.getByText('Firmen-Info')).toBeTruthy();
        expect(screen.getAllByText('Development').length).toBeGreaterThan(0);
        expect(screen.getByText('dev')).toBeTruthy();
        expect(screen.getByText('dev-tenant-id')).toBeTruthy();
        expect(screen.getByText(/25\.04\.2026/)).toBeTruthy();
        expect(screen.getByText('Aktiv')).toBeTruthy();
    });

    it('returns null when tenant prop is missing', () => {
        const { container } = render(<FirmenInfo tenant={null} />);
        expect(container.firstChild).toBeNull();
    });

    it('shows loading spinner when loading prop is true', () => {
        render(<FirmenInfo tenant={null} loading />);
        expect(screen.getByLabelText('Laden')).toBeTruthy();
    });

    it('shows error alert when error prop is set', () => {
        render(<FirmenInfo tenant={null} error={new Error('Network failed')} />);
        expect(screen.getByText('Fehler beim Laden')).toBeTruthy();
        expect(screen.getByText('Network failed')).toBeTruthy();
    });
});
