export type TenantLicenseKind = 'none' | 'trial' | 'valid' | 'expired';

export type TenantLicenseLabel = {
    kind: TenantLicenseKind;
    /** de-AT short label for table cells */
    label: string;
    daysRemaining: number | null;
};

export function resolveTenantLicenseLabel(
    licenseValidUntilUtc: string | null | undefined,
    licenseKey: string | null | undefined,
    now = Date.now(),
    serverDaysRemaining?: number | null,
): TenantLicenseLabel {
    const serverDays =
        typeof serverDaysRemaining === 'number' && Number.isFinite(serverDaysRemaining)
            ? Math.trunc(serverDaysRemaining)
            : null;

    if (!licenseValidUntilUtc?.trim()) {
        if (serverDays != null) {
            if (serverDays < 0) {
                return { kind: 'expired', label: 'Abgelaufen', daysRemaining: serverDays };
            }

            const isTrial = !licenseKey?.trim() || serverDays <= 31;
            if (isTrial) {
                return {
                    kind: 'trial',
                    label: `Demo (${serverDays} T.)`,
                    daysRemaining: serverDays,
                };
            }

            return { kind: 'valid', label: '—', daysRemaining: serverDays };
        }

        if (licenseKey?.trim()) {
            return { kind: 'valid', label: '—', daysRemaining: null };
        }
        return { kind: 'none', label: '—', daysRemaining: null };
    }

    const until = new Date(licenseValidUntilUtc);
    const diffMs = until.getTime() - now;
    const daysRemaining = serverDays ?? Math.ceil(diffMs / (24 * 60 * 60 * 1000));

    if (daysRemaining < 0) {
        return { kind: 'expired', label: 'Abgelaufen', daysRemaining };
    }

    const isTrial = !licenseKey?.trim() || daysRemaining <= 31;
    if (isTrial) {
        return {
            kind: 'trial',
            label: `Demo (${daysRemaining} T.)`,
            daysRemaining,
        };
    }

    return {
        kind: 'valid',
        label: until.toLocaleDateString('de-AT'),
        daysRemaining,
    };
}
