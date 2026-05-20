import { AXIOS_INSTANCE } from '@/lib/axios';
import { authStorage } from '@/features/auth/services/authStorage';
import { beginTenantSwitch } from '@/features/auth/services/tenantSwitchController';
import { writeDevTenantSlug } from '@/features/auth/services/devTenant';
import { buildTenantSubdomainOrigin } from '@/lib/auth/impersonationHandoff';
import {
    buildImpersonationRedirectUrl,
    shouldUseProductionImpersonationRedirect,
} from '@/lib/auth/tokenHandler';

export type AdminTenantListItem = {
    id: string;
    name: string;
    slug: string;
    email?: string | null;
    phone?: string | null;
    status: string;
    isActive: boolean;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
    createdAt: string;
    updatedAt?: string | null;
};

export type TenantProvisioning = {
    cashRegisterId: string;
    cashRegisterNumber: string;
    adminUserId: string;
    adminEmail: string;
    generatedPassword: string;
    categoryId: string;
    productIds: string[];
    trialLicenseValidUntilUtc?: string | null;
};

/** Public tenant FA URL for onboarding hints (https://{slug}.regkasse.at). */
export function buildTenantPortalUrl(slug: string): string {
    return buildTenantSubdomainOrigin(slug);
}

/** Copy-paste block for handing off provisioned admin credentials (German operator text). */
export function formatTenantProvisioningHandoff(
    tenantName: string,
    slug: string,
    provisioning: TenantProvisioning,
): string {
    const portalUrl = buildTenantPortalUrl(slug);
    const trialLine = provisioning.trialLicenseValidUntilUtc
        ? `Demo-Lizenz: 30 Tage gültig (bis ${new Date(provisioning.trialLicenseValidUntilUtc).toLocaleDateString('de-AT')})`
        : '';
    return [
        `Mandant "${tenantName}" wurde erfolgreich erstellt!`,
        '',
        'Zugangsdaten für den Administrator:',
        `E-Mail: ${provisioning.adminEmail}`,
        `Passwort: ${provisioning.generatedPassword}`,
        '',
        trialLine,
        trialLine ? '' : null,
        `Standardkasse: Hauptkasse (${provisioning.cashRegisterNumber})`,
        `Demo-Produkte: ${provisioning.productIds.length} Stück wurden angelegt`,
        '',
        'Nächste Schritte:',
        '1. Melden Sie sich mit den obigen Zugangsdaten an',
        `2. Wechseln Sie zu ${portalUrl}`,
        '3. Passen Sie Produkte, Steuern und Einstellungen an',
    ]
        .filter((line): line is string => line != null && line !== '')
        .join('\n');
}

export type AdminTenantDetail = AdminTenantListItem & {
    address?: string | null;
    deletedAtUtc?: string | null;
    provisioning?: TenantProvisioning | null;
};

export type TenantSlugAvailability = {
    normalizedSlug: string;
    isValid: boolean;
    available: boolean;
};

export type CreateAdminTenantRequest = {
    name: string;
    slug: string;
    email?: string | null;
    phone?: string | null;
    address?: string | null;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
    adminEmail?: string | null;
    adminPassword?: string | null;
    grantTrialLicense?: boolean;
};

export type UpdateAdminTenantRequest = {
    name?: string | null;
    email?: string | null;
    phone?: string | null;
    address?: string | null;
    status?: string | null;
    licenseKey?: string | null;
    licenseValidUntilUtc?: string | null;
    isActive?: boolean | null;
};

export type TenantImpersonationResponse = {
    token: string;
    expiresIn: number;
    refreshToken?: string | null;
    refreshTokenExpiresAtUtc?: string | null;
    tenantId: string;
    tenantSlug: string;
    tenantDisplayName?: string | null;
    impersonation: boolean;
};

export async function checkAdminTenantSlugAvailability(slug: string): Promise<TenantSlugAvailability> {
    const { data } = await AXIOS_INSTANCE.get<TenantSlugAvailability>('/api/admin/tenants/slug-availability', {
        params: { slug },
    });
    return data;
}

export async function listAdminTenants(includeDeleted = false): Promise<AdminTenantListItem[]> {
    const { data } = await AXIOS_INSTANCE.get<AdminTenantListItem[]>('/api/admin/tenants', {
        params: { includeDeleted },
    });
    return data;
}

export async function getAdminTenantById(tenantId: string): Promise<AdminTenantDetail> {
    const { data } = await AXIOS_INSTANCE.get<AdminTenantDetail>(`/api/admin/tenants/${tenantId}`);
    return data;
}

export async function createAdminTenant(body: CreateAdminTenantRequest): Promise<AdminTenantDetail> {
    const { data } = await AXIOS_INSTANCE.post<AdminTenantDetail>('/api/admin/tenants', body);
    return data;
}

export async function updateAdminTenant(
    tenantId: string,
    body: UpdateAdminTenantRequest,
): Promise<AdminTenantDetail> {
    const { data } = await AXIOS_INSTANCE.put<AdminTenantDetail>(`/api/admin/tenants/${tenantId}`, body);
    return data;
}

export async function deleteAdminTenant(tenantId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}`);
}

export async function impersonateAdminTenant(tenantId: string): Promise<TenantImpersonationResponse> {
    const { data } = await AXIOS_INSTANCE.post<TenantImpersonationResponse>(
        `/api/admin/tenants/${tenantId}/impersonate`,
    );
    return data;
}

/**
 * Applies impersonation session.
 * Production: redirect to tenant subdomain with JWT in URL fragment (not stored on admin host).
 * Development: store token + dev tenant override on same origin and reload.
 */
export function applyTenantImpersonationSession(res: TenantImpersonationResponse): void {
    if (typeof window === 'undefined') {
        return;
    }

    beginTenantSwitch();

    if (shouldUseProductionImpersonationRedirect()) {
        window.location.assign(buildImpersonationRedirectUrl(res));
        return;
    }

    authStorage.setToken(res.token);
    if (res.refreshToken) {
        authStorage.setRefreshToken(res.refreshToken);
    }
    if (!writeDevTenantSlug(res.tenantSlug)) {
        window.location.reload();
    }
}
