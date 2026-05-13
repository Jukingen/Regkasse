import { AXIOS_INSTANCE } from '@/lib/axios';

/** Anonymous POS-aligned snapshot from <c>GET /api/license/status</c>. */
export type LicensePublicStatusDto = {
    licenseType: 'Trial' | 'Paid' | string;
    validUntil: string | null;
    daysRemaining: number;
    features: string[];
    isExpired: boolean;
    isValid: boolean;
};

export type LicenseStatusResponse = {
    isValid: boolean;
    isTrial: boolean;
    isExpired: boolean;
    daysRemaining: number;
    /** ISO 8601 UTC timestamp; null when license has no exp claim (effectively unlimited paid). */
    expiryDate: string | null;
    machineHash: string;
};

export type ActivateLicenseRequest = {
    licenseKey: string;
    offlineActivationJwt?: string | null;
};

export type LicenseActivationResult = {
    success: boolean;
    message?: string | null;
    /** ISO 8601 UTC when activation succeeded (backend). */
    validUntil?: string | null;
};

export type GenerateLicenseRequest = {
    customerName: string;
    /** Date-only (YYYY-MM-DD) interpreted as end-of-day UTC by the backend. */
    expiryDate: string;
    /** Sent as JSON bindToMachineFingerprint (backend merges with legacy requireFingerprint). */
    bindToMachineFingerprint: boolean;
    /** Required when bindToMachineFingerprint=true; lowercase hex SHA-256 (64 chars). */
    machineHashHex?: string | null;
};

export type GenerateLicenseResponse = {
    success: boolean;
    licenseKey: string | null;
    signedJwt: string | null;
    /** Alias returned by some API versions; same as signedJwt. */
    licenseJwt?: string | null;
    expiryAtUtc: string | null;
    message?: string | null;
};

export type IssuedLicenseListItemDto = {
    id: string;
    licenseKey: string;
    customerName: string;
    expiryAtUtc: string;
    requireFingerprint: boolean;
    machineHashHex: string | null;
    issuedAtUtc: string;
    issuedByUserId: string | null;
    isRevoked: boolean;
    revokedAtUtc: string | null;
    revocationReason: string | null;
    supersededByLicenseId?: string | null;
};

export type IssuedLicensesListResponse = {
    total: number;
    pageNumber: number;
    pageSize: number;
    items: IssuedLicenseListItemDto[];
};

export type IssuedLicensesListParams = {
    /** Customer name substring (optional). */
    search?: string | null;
    pageNumber?: number;
    pageSize?: number;
};

export const licenseQueryKeys = {
    status: ['admin', 'license', 'status'] as const,
    /** GET /api/license/status (anonymous, POS contract). */
    publicStatus: ['admin', 'license', 'publicStatus'] as const,
    listRoot: ['admin', 'license', 'list'] as const,
    list: (params: IssuedLicensesListParams) => [...licenseQueryKeys.listRoot, params] as const,
};

export async function getPublicLicenseStatus(): Promise<LicensePublicStatusDto> {
    const { data } = await AXIOS_INSTANCE.get<LicensePublicStatusDto>('/api/license/status');
    return data;
}

export async function getLicenseStatus(): Promise<LicenseStatusResponse> {
    const { data } = await AXIOS_INSTANCE.get<LicenseStatusResponse>('/api/admin/license/status');
    return data;
}

export async function postActivateLicense(body: ActivateLicenseRequest): Promise<LicenseActivationResult> {
    const { data } = await AXIOS_INSTANCE.post<LicenseActivationResult>('/api/admin/license/activate', body);
    return data;
}

export async function postGenerateLicense(body: GenerateLicenseRequest): Promise<GenerateLicenseResponse> {
    const bind = body.bindToMachineFingerprint;
    const payload = {
        customerName: body.customerName,
        expiryDate: body.expiryDate,
        bindToMachineFingerprint: bind,
        /** Legacy/alternate binding flag — backend uses `bindToMachineFingerprint ?? requireFingerprint`. */
        requireFingerprint: bind,
        machineHashHex: body.machineHashHex?.trim() ? body.machineHashHex.trim().toLowerCase() : undefined,
    };
    const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>('/api/admin/license/generate', payload);
    return data;
}

export async function getIssuedLicensesList(
    params: IssuedLicensesListParams | undefined,
): Promise<IssuedLicensesListResponse> {
    const { data } = await AXIOS_INSTANCE.get<IssuedLicensesListResponse>('/api/admin/license/list', {
        params: {
            search: params?.search?.trim() ? params.search.trim() : undefined,
            pageNumber: params?.pageNumber ?? 1,
            pageSize: params?.pageSize ?? 50,
        },
    });
    return data;
}

/** DELETE /api/admin/license/revoke/{id} */
export async function deleteRevokeIssuedLicense(id: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/license/revoke/${encodeURIComponent(id)}`);
}

/** POST /api/admin/license/renew — supply full `licenseKey` or list row `issuedLicenseId`, not both. */
export type RenewLicenseRequest = {
    licenseKey?: string;
    issuedLicenseId?: string;
    newExpiryDate: string;
};

export async function postRenewLicense(body: RenewLicenseRequest): Promise<GenerateLicenseResponse> {
    const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>('/api/admin/license/renew', body);
    return data;
}

/** POST /api/admin/license/upgrade — supply full `licenseKey` or list row `issuedLicenseId`, not both. */
export type UpgradeIssuedLicenseRequest = {
    licenseKey?: string;
    issuedLicenseId?: string;
    newExpiryDate: string;
    reason?: string | null;
};

export async function postUpgradeIssuedLicense(body: UpgradeIssuedLicenseRequest): Promise<GenerateLicenseResponse> {
    const payload: Record<string, string> = { newExpiryDate: body.newExpiryDate };
    if (body.licenseKey?.trim()) {
        payload.licenseKey = body.licenseKey.trim();
    }
    if (body.issuedLicenseId) {
        payload.issuedLicenseId = body.issuedLicenseId;
    }
    if (body.reason?.trim()) {
        payload.reason = body.reason.trim();
    }
    const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>('/api/admin/license/upgrade', payload);
    return data;
}
