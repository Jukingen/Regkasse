import { describe, expect, it } from 'vitest';

import type { TenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';
import {
    getHeaderLicenseStatusClass,
    getHeaderLicenseStatusText,
    getHeaderLicenseTooltip,
} from '../headerLicenseStatus';

const t = (key: string, params?: Record<string, string | number>) => {
    const table: Record<string, string> = {
        'license.badge.headerShort.none': 'Keine Mandantenlizenz',
        'license.badge.headerShort.expired': 'Lizenz abgelaufen',
        'license.badge.headerShort.expiringSoon': 'Lizenz läuft bald ab',
        'license.badge.headerShort.licensed': 'Lizenziert',
        'license.badge.headerShort.mandantTooltip': `Mandantenlizenz: ${params?.status ?? ''}`,
    };
    return table[key] ?? key;
};

describe('headerLicenseStatus', () => {
    it('maps missing mandant license to expired class and keine Mandantenlizenz text', () => {
        const license: TenantLicenseLabel = { kind: 'none', label: '—', daysRemaining: null };
        expect(getHeaderLicenseStatusClass(license, null)).toBe('expired');
        expect(getHeaderLicenseStatusText(license, t, null)).toBe('Keine Mandantenlizenz');
    });

    it('maps expired mandant license to expired class and abgelaufen text', () => {
        const license: TenantLicenseLabel = { kind: 'expired', label: 'Abgelaufen', daysRemaining: -5 };
        expect(getHeaderLicenseStatusClass(license, '2026-01-01T00:00:00Z')).toBe('expired');
        expect(getHeaderLicenseStatusText(license, t, '2026-01-01T00:00:00Z')).toBe('Lizenz abgelaufen');
    });

    it('maps seven or fewer days to warning class and expiring soon text', () => {
        const license: TenantLicenseLabel = { kind: 'trial', label: 'Demo (5 T.)', daysRemaining: 5 };
        expect(getHeaderLicenseStatusClass(license, '2026-05-27T00:00:00Z')).toBe('warning');
        expect(getHeaderLicenseStatusText(license, t, '2026-05-27T00:00:00Z')).toBe('Lizenz läuft bald ab');
    });

    it('maps more than seven days to valid class and lizenziert text', () => {
        const license: TenantLicenseLabel = { kind: 'valid', label: '31.08.2026', daysRemaining: 30 };
        expect(getHeaderLicenseStatusClass(license, '2026-08-31T00:00:00Z')).toBe('valid');
        expect(getHeaderLicenseStatusText(license, t, '2026-08-31T00:00:00Z')).toBe('Lizenziert');
    });

    it('prefixes tooltip with Mandantenlizenz', () => {
        const license: TenantLicenseLabel = { kind: 'valid', label: '31.08.2026', daysRemaining: 30 };
        expect(getHeaderLicenseTooltip(license, t, '2026-08-31T00:00:00Z')).toBe(
            'Mandantenlizenz: Lizenziert',
        );
    });
});
