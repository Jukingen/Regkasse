import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import { describe, expect, it } from 'vitest';

import { resolveTenantLicenseBadgeDisplay } from '../resolveTenantLicenseBadgeDisplay';

dayjs.extend(utc);

describe('resolveTenantLicenseBadgeDisplay', () => {
    const now = dayjs.utc('2026-05-20T12:00:00Z');
    const t = (key: string, params?: Record<string, string | number>) => {
        switch (key) {
            case 'license.phase.labels.active':
                return 'Aktiv';
            case 'license.phase.labels.graceWrite':
                return 'Grace-Phase: Schreiben erlaubt';
            case 'license.phase.labels.graceReadonly':
                return 'Grace-Phase: Nur Lesen';
            case 'license.phase.labels.lockdown':
                return 'Lockdown';
            case 'license.phase.labels.noLicense':
                return 'Keine Lizenz';
            case 'license.phase.daysExpired':
                return `Seit ${params?.days} Tagen abgelaufen`;
            case 'license.phase.daysRemaining':
                return `${params?.days} Tage verbleibend`;
            case 'license.phase.messages.tenant.active':
                return 'Mandantenlizenz ist aktiv.';
            case 'license.phase.messages.tenant.graceWrite':
                return `Mandantenlizenz ist seit ${params?.days} Tagen abgelaufen.`;
            case 'license.phase.messages.tenant.graceReadonly':
                return `Mandantenlizenz ist seit ${params?.days} Tagen abgelaufen. Schreiben ist gesperrt.`;
            case 'license.phase.messages.tenant.lockdown':
                return `Mandantenlizenz ist seit ${params?.days} Tagen abgelaufen. Lockdown.`;
            case 'license.phase.messages.tenant.noLicense':
                return 'Keine Mandantenlizenz hinterlegt.';
            default:
                return key;
        }
    };

    it('shows no-license badge when no key and no end date', () => {
        expect(resolveTenantLicenseBadgeDisplay(null, null, t, now)).toEqual({
            label: 'Keine Lizenz',
            color: 'default',
            tooltip: 'Keine Mandantenlizenz hinterlegt.',
        });
    });

    it('shows active badge for a valid paid license', () => {
        expect(
            resolveTenantLicenseBadgeDisplay('2026-07-20T00:00:00Z', 'REGK-KEY', t, now),
        ).toEqual({
            label: 'Aktiv',
            color: 'green',
            tooltip: 'Mandantenlizenz ist aktiv. 61 Tage verbleibend',
        });
    });

    it('shows grace-write badge for recently expired licenses', () => {
        expect(
            resolveTenantLicenseBadgeDisplay('2026-05-10T00:00:00Z', 'REGK-KEY', t, now),
        ).toEqual({
            label: 'Grace-Phase: Schreiben erlaubt',
            color: 'gold',
            tooltip: 'Mandantenlizenz ist seit 10 Tagen abgelaufen. Seit 10 Tagen abgelaufen',
        });
    });

    it('shows grace-readonly badge after tenant write grace ends', () => {
        expect(
            resolveTenantLicenseBadgeDisplay('2026-04-05T00:00:00Z', 'REGK-KEY', t, now),
        ).toEqual({
            label: 'Grace-Phase: Nur Lesen',
            color: 'orange',
            tooltip: 'Mandantenlizenz ist seit 45 Tagen abgelaufen. Schreiben ist gesperrt. Seit 45 Tagen abgelaufen',
        });
    });

    it('shows lockdown badge for heavily expired licenses', () => {
        expect(
            resolveTenantLicenseBadgeDisplay('2026-01-15T00:00:00Z', 'REGK-KEY', t, now),
        ).toEqual({
            label: 'Lockdown',
            color: 'red',
            tooltip: 'Mandantenlizenz ist seit 125 Tagen abgelaufen. Lockdown. Seit 125 Tagen abgelaufen',
        });
    });
});
