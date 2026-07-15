import { describe, expect, it } from 'vitest';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
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
        'license.badge.headerShort.daysRemaining': `${params?.days ?? 0} Tage`,
        'license.badge.headerShort.licensed': 'Lizenziert',
        'license.badge.headerShort.mandantTooltip': `Mandantenlizenz: ${params?.status ?? ''}`,
        'license.phase.labels.active': 'Aktiv',
    };
    return table[key] ?? key;
};

function status(partial: Partial<ResolvedLicenseStatus> & Pick<ResolvedLicenseStatus, 'kind'>): ResolvedLicenseStatus {
    return {
        daysRemaining: 0,
        daysExpired: 0,
        canWrite: false,
        canManageUsers: false,
        canAccess: false,
        ...partial,
    };
}

describe('headerLicenseStatus', () => {
    it('maps missing mandant license to expired class and keine Mandantenlizenz text', () => {
        const license = status({ kind: 'no_license' });
        expect(getHeaderLicenseStatusClass(license)).toBe('expired');
        expect(getHeaderLicenseStatusText(license, t)).toBe('Keine Mandantenlizenz');
    });

    it('maps active license with days remaining to Aktiv (N Tage)', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 999,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseStatusClass(license)).toBe('valid');
        expect(getHeaderLicenseStatusText(license, t)).toBe('Aktiv (999 Tage)');
    });

    it('maps expired mandant license to expired class and abgelaufen text', () => {
        const license = status({ kind: 'expired', daysRemaining: -5, daysExpired: 5 });
        expect(getHeaderLicenseStatusClass(license)).toBe('expired');
        expect(getHeaderLicenseStatusText(license, t)).toBe('Lizenz abgelaufen');
    });

    it('maps seven or fewer days to warning class and expiring soon text', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 5,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseStatusClass(license)).toBe('warning');
        expect(getHeaderLicenseStatusText(license, t)).toBe('Lizenz läuft bald ab');
    });

    it('maps more than seven days to valid class and Aktiv (N Tage) text', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 30,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseStatusClass(license)).toBe('valid');
        expect(getHeaderLicenseStatusText(license, t)).toBe('Aktiv (30 Tage)');
    });

    it('prefixes tooltip with Mandantenlizenz', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 30,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseTooltip(license, t)).toBe('Mandantenlizenz: Aktiv (30 Tage)');
    });
});
