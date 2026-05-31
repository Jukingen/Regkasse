import type { LicenseStatusResponse } from '@/api/manual/adminLicense';
import type { TenantLicenseStatus as TenantLicenseStatusDto } from '@/features/super-admin/api/adminTenantLicense';
import {
    DEPLOYMENT_GRACE_WRITE_DAYS,
    DEPLOYMENT_LOCKDOWN_DAYS,
    TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

const DAY_MS = 24 * 60 * 60 * 1000;

export type LicenseStatusKind =
    | 'active'
    | 'grace_write'
    | 'grace_readonly'
    | 'lockdown'
    | 'expired'
    | 'no_license';

export type LicenseStatusScope = 'tenant' | 'deployment';

export type ResolvedLicenseStatus = {
    kind: LicenseStatusKind;
    daysRemaining: number;
    daysExpired: number;
    canWrite: boolean;
    canManageUsers: boolean;
    canAccess: boolean;
};

type TenantLicenseInput = Pick<
    TenantLicenseStatusDto,
    'daysRemaining' | 'kind' | 'licenseKey' | 'validUntilUtc'
>;

type TenantLicenseRowInput = {
    licenseDaysRemaining?: number | null;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
};

function isFiniteNumber(value: unknown): value is number {
    return typeof value === 'number' && Number.isFinite(value);
}

function normalizeLicenseKind(kind: string | null | undefined): LicenseStatusKind | null {
    switch (kind?.trim().toLowerCase()) {
        case 'active':
            return 'active';
        case 'grace_write':
            return 'grace_write';
        case 'grace_read_only':
        case 'grace_readonly':
            return 'grace_readonly';
        case 'lockdown':
            return 'lockdown';
        case 'expired':
            return 'expired';
        case 'no_license':
            return 'no_license';
        default:
            return null;
    }
}

function getSignedDaysRemaining(
    validUntilUtc: string | null | undefined,
    fallback: number | null | undefined,
    nowMs: number,
): number {
    if (isFiniteNumber(fallback)) {
        return Math.trunc(fallback);
    }

    if (!validUntilUtc?.trim()) {
        return 0;
    }

    const expiresAtMs = new Date(validUntilUtc).getTime();
    if (!Number.isFinite(expiresAtMs)) {
        return 0;
    }

    return Math.ceil((expiresAtMs - nowMs) / DAY_MS);
}

function getPositiveDaysExpired(validUntilUtc: string | null | undefined, nowMs: number): number {
    if (!validUntilUtc?.trim()) {
        return 0;
    }

    const expiresAtMs = new Date(validUntilUtc).getTime();
    if (!Number.isFinite(expiresAtMs) || expiresAtMs >= nowMs) {
        return 0;
    }

    return Math.max(0, Math.floor((nowMs - expiresAtMs) / DAY_MS));
}

function buildStatus(
    kind: LicenseStatusKind,
    daysRemaining: number,
    permissions: Pick<ResolvedLicenseStatus, 'canAccess' | 'canManageUsers' | 'canWrite'>,
): ResolvedLicenseStatus {
    const daysExpired = daysRemaining < 0 ? Math.abs(daysRemaining) : 0;
    return {
        kind,
        daysRemaining,
        daysExpired,
        ...permissions,
    };
}

function getTenantPermissions(kind: LicenseStatusKind): Pick<
    ResolvedLicenseStatus,
    'canAccess' | 'canManageUsers' | 'canWrite'
> {
    switch (kind) {
        case 'active':
        case 'grace_write':
            return { canWrite: true, canManageUsers: true, canAccess: true };
        case 'grace_readonly':
            return { canWrite: false, canManageUsers: true, canAccess: true };
        case 'lockdown':
        case 'expired':
        case 'no_license':
        default:
            return { canWrite: false, canManageUsers: false, canAccess: false };
    }
}

function getDeploymentPermissions(kind: LicenseStatusKind): Pick<
    ResolvedLicenseStatus,
    'canAccess' | 'canManageUsers' | 'canWrite'
> {
    switch (kind) {
        case 'active':
        case 'grace_write':
            return { canWrite: true, canManageUsers: true, canAccess: true };
        case 'grace_readonly':
            return { canWrite: false, canManageUsers: false, canAccess: true };
        case 'lockdown':
        case 'expired':
        case 'no_license':
        default:
            return { canWrite: false, canManageUsers: false, canAccess: false };
    }
}

function resolveTenantKind(daysRemaining: number, explicitKind: LicenseStatusKind | null): LicenseStatusKind {
    if (explicitKind) {
        return explicitKind;
    }

    if (daysRemaining >= 0) {
        return 'active';
    }

    const daysExpired = Math.abs(daysRemaining);
    if (daysExpired <= TENANT_GRACE_PERIOD_DAYS) {
        return 'grace_write';
    }
    return 'lockdown';
}

export function resolveTenantLicenseStatus(
    input: TenantLicenseInput | null | undefined,
    nowMs = Date.now(),
): ResolvedLicenseStatus {
    if (!input?.validUntilUtc?.trim() && !isFiniteNumber(input?.daysRemaining)) {
        return buildStatus('no_license', 0, getTenantPermissions('no_license'));
    }

    const daysRemaining = getSignedDaysRemaining(input?.validUntilUtc, input?.daysRemaining, nowMs);
    const explicitKind = normalizeLicenseKind(input?.kind);
    const kind =
        explicitKind === 'no_license' && input?.validUntilUtc?.trim()
            ? resolveTenantKind(daysRemaining, null)
            : explicitKind ?? resolveTenantKind(daysRemaining, null);

    return buildStatus(kind, daysRemaining, getTenantPermissions(kind));
}

export function resolveTenantRowLicenseStatus(
    input: TenantLicenseRowInput | null | undefined,
    nowMs = Date.now(),
): ResolvedLicenseStatus {
    return resolveTenantLicenseStatus(
        {
            kind: undefined,
            licenseKey: input?.licenseKey,
            validUntilUtc: input?.licenseValidUntilUtc,
            daysRemaining: input?.licenseDaysRemaining,
        },
        nowMs,
    );
}

export function resolveDeploymentLicenseStatus(
    snapshot: LicenseStatusResponse | null | undefined,
    nowMs = Date.now(),
): ResolvedLicenseStatus {
    if (!snapshot) {
        return buildStatus('no_license', 0, getDeploymentPermissions('no_license'));
    }

    if (snapshot.isValid || (snapshot.isTrial && !snapshot.isExpired)) {
        const daysRemaining = isFiniteNumber(snapshot.daysRemaining)
            ? Math.max(0, Math.trunc(snapshot.daysRemaining))
            : getSignedDaysRemaining(snapshot.expiryDate, null, nowMs);
        return buildStatus('active', daysRemaining, getDeploymentPermissions('active'));
    }

    if (!snapshot.expiryDate) {
        return buildStatus('no_license', 0, getDeploymentPermissions('no_license'));
    }

    const daysExpired = getPositiveDaysExpired(snapshot.expiryDate, nowMs);
    const signedDaysRemaining = daysExpired > 0 ? -daysExpired : 0;

    if (daysExpired <= DEPLOYMENT_GRACE_WRITE_DAYS) {
        return buildStatus('grace_write', signedDaysRemaining, getDeploymentPermissions('grace_write'));
    }

    if (daysExpired <= DEPLOYMENT_LOCKDOWN_DAYS) {
        return buildStatus(
            'grace_readonly',
            signedDaysRemaining,
            getDeploymentPermissions('grace_readonly'),
        );
    }

    return buildStatus('lockdown', signedDaysRemaining, getDeploymentPermissions('lockdown'));
}

export function getLicenseStatusTagColor(kind: LicenseStatusKind): string {
    switch (kind) {
        case 'active':
            return 'green';
        case 'grace_write':
            return 'gold';
        case 'grace_readonly':
            return 'orange';
        case 'lockdown':
        case 'expired':
            return 'red';
        case 'no_license':
        default:
            return 'default';
    }
}

export function getLicenseStatusLabel(
    kind: LicenseStatusKind,
    t: TranslateFn,
): string {
    switch (kind) {
        case 'active':
            return t('license.phase.labels.active');
        case 'grace_write':
            return t('license.phase.labels.graceWrite');
        case 'grace_readonly':
            return t('license.phase.labels.graceReadonly');
        case 'lockdown':
            return t('license.phase.labels.lockdown');
        case 'expired':
            return t('license.phase.labels.expired');
        case 'no_license':
        default:
            return t('license.phase.labels.noLicense');
    }
}

export function getLicenseStatusMessage(
    status: ResolvedLicenseStatus,
    scope: LicenseStatusScope,
    t: TranslateFn,
): string {
    const root = scope === 'tenant' ? 'license.phase.messages.tenant' : 'license.phase.messages.deployment';

    switch (status.kind) {
        case 'active':
            return t(`${root}.active`);
        case 'grace_write':
            return t(`${root}.graceWrite`, { days: status.daysExpired });
        case 'grace_readonly':
            return t(`${root}.graceReadonly`, { days: status.daysExpired });
        case 'lockdown':
            return t(`${root}.lockdown`, { days: status.daysExpired });
        case 'expired':
            return t(`${root}.expired`, { days: status.daysExpired });
        case 'no_license':
        default:
            return t(`${root}.noLicense`);
    }
}

export function getLicenseStatusDayText(
    status: ResolvedLicenseStatus,
    t: TranslateFn,
): string | null {
    if (status.daysExpired > 0) {
        return t('license.phase.daysExpired', { days: status.daysExpired });
    }

    if (status.daysRemaining > 0) {
        return t('license.phase.daysRemaining', { days: status.daysRemaining });
    }

    return null;
}
