import { describe, expect, it } from 'vitest';

import type { ResolvedLicenseStatus } from '@/features/license/utils/licenseStatus';
import {
    getHeaderLicenseStatusClass,
    getHeaderLicenseStatusText,
    getHeaderLicenseTooltip,
    getHeaderLicenseTooltipStatusLabel,
    hasDetailedHeaderLicenseTooltip,
} from '../headerLicenseStatus';

const t = (key: string, params?: Record<string, string | number>) => {
    const table: Record<string, string> = {
        'license.badge.headerShort.none': 'Keine Mandantenlizenz',
        'license.badge.headerShort.expired': 'Lizenz abgelaufen',
        'license.badge.headerShort.expiringSoon': 'Lizenz läuft bald ab',
        'license.badge.headerShort.expiringSoonWithDays': `Läuft ab in ${params?.days ?? 0} Tagen`,
        'license.badge.headerShort.expiringSoonWithHours': `Läuft ab in ${params?.hours ?? 0} Std.`,
        'license.badge.headerShort.validUntilTooltip': `Gültig bis: ${params?.dateTime ?? ''}`,
        'license.badge.headerShort.expiredAtTooltip': `Abgelaufen am: ${params?.dateTime ?? ''}`,
        'license.badge.headerShort.tooltip.ariaSummary': `Gültig bis: ${params?.dateTime ?? ''}. Verbleibende Tage: ${params?.days ?? 0}. Status: ${params?.status ?? ''}.`,
        'license.badge.headerShort.tooltip.validUntil': 'Gültig bis',
        'license.badge.headerShort.tooltip.daysRemaining': 'Verbleibende Tage',
        'license.badge.headerShort.tooltip.status': 'Status',
        'license.phase.labels.expired': 'Abgelaufen',
        'license.badge.headerShort.daysRemaining': `${params?.days ?? 0} Tage`,
        'license.badge.headerShort.licensed': 'Lizenziert',
        'license.badge.headerShort.mandantTooltip': `Mandantenlizenz: ${params?.status ?? ''}`,
        'license.phase.labels.active': 'Aktiv',
    };
    return table[key] ?? key;
};

const VALID_UNTIL = '2026-07-20T21:30:00.000Z';

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

    it('maps seven or fewer days to warning class and expiring soon text with days', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 5,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseStatusClass(license)).toBe('warning');
        expect(getHeaderLicenseStatusText(license, t, { validUntilUtc: VALID_UNTIL })).toBe('Läuft ab in 5 Tagen');
    });

    it('shows hours remaining when less than one day left', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 0,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        const validUntil = new Date(Date.now() + 6 * 60 * 60 * 1000).toISOString();
        expect(getHeaderLicenseStatusText(license, t, { validUntilUtc: validUntil })).toBe('Läuft ab in 6 Std.');
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

    it('prefixes tooltip with Mandantenlizenz for active licenses without expiry date', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 30,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        expect(getHeaderLicenseTooltip(license, t)).toBe('Mandantenlizenz: Aktiv (30 Tage)');
    });

    it('shows detailed aria summary in tooltip when valid-until is known', () => {
        const license = status({
            kind: 'active',
            daysRemaining: 5,
            canWrite: true,
            canManageUsers: true,
            canAccess: true,
        });
        const tooltip = getHeaderLicenseTooltip(license, t, { validUntilUtc: VALID_UNTIL });
        expect(tooltip).toContain('Gültig bis:');
        expect(tooltip).toContain('Verbleibende Tage: 5');
        expect(tooltip).toContain('Status: Aktiv');
        expect(hasDetailedHeaderLicenseTooltip({ validUntilUtc: VALID_UNTIL })).toBe(true);
    });

    it('shows expired status in detailed tooltip summary', () => {
        const license = status({ kind: 'expired', daysRemaining: -5, daysExpired: 5 });
        expect(getHeaderLicenseTooltipStatusLabel(license, t)).toBe('Abgelaufen');
        const tooltip = getHeaderLicenseTooltip(license, t, { validUntilUtc: VALID_UNTIL });
        expect(tooltip).toContain('Verbleibende Tage: 0');
        expect(tooltip).toContain('Status: Abgelaufen');
    });
});
