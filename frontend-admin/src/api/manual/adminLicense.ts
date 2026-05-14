import { AXIOS_INSTANCE } from '@/lib/axios';

/** Anonymous POS/FA snapshot from <c>GET /api/license/status</c>. */
export type LicensePublicStatusDto = {
    /** Trial, Licensed, Expired, or Demo (development snapshot). */
    licenseType: 'Trial' | 'Licensed' | 'Expired' | 'Demo' | 'Paid' | string;
    /** ISO 8601 UTC from backend <c>DateTime?</c> JSON. */
    validUntil: string | null;
    daysRemaining: number;
    features: string[];
    isExpired: boolean;
    isValid: boolean;
    /** Demo (local dev bypass), Trial, or Production. */
    mode?: 'Demo' | 'Trial' | 'Production' | string;
};

export type LicenseStatusResponse = {
    isValid: boolean;
    isTrial: boolean;
    isExpired: boolean;
    daysRemaining: number;
    /** ISO 8601 UTC timestamp; null when license has no exp claim (effectively unlimited paid). */
    expiryDate: string | null;
    machineHash: string;
    /** Enabled license feature ids; omitted on older servers (treat as full bundle). */
    enabledFeatures?: string[] | null;
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
    /** Optional explicit feature bundle; omitted = full single-license bundle. */
    features?: string[] | null;
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
    transferredToLicenseId?: string | null;
    /** Distinct machines in `activated_licenses` for this license key. */
    activatedDeviceCount?: number;
    /** Latest activation timestamp (UTC) across devices. */
    lastActivationAtUtc?: string | null;
    /** Shortened SHA-256 hex (first 8 + last 8) for the device with the latest `last_seen`. */
    recentMachineFingerprintShort?: string | null;
    isCancelled?: boolean;
    isDeleted?: boolean;
    /** Enabled feature ids when stored on the issuance row. */
    features?: string[] | null;
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
    /** Substring match on activated machine fingerprint (SHA-256 hex, optional). */
    machineFingerprint?: string | null;
    pageNumber?: number;
    pageSize?: number;
};

export type LicenseActivationAttemptsListParams = {
    licenseKey?: string | null;
    fromUtc?: string | null;
    toUtc?: string | null;
    status?: string | null;
    machineFingerprint?: string | null;
    pageNumber?: number;
    pageSize?: number;
};

export type LicenseActivationAttemptListItemDto = {
    id: string;
    licenseKeyMasked: string;
    machineFingerprint: string;
    activationStatus: string;
    failureReason: string | null;
    clientIp: string | null;
    userAgent: string | null;
    activatedAtUtc: string;
    deactivatedAtUtc: string | null;
};

export type LicenseActivationAttemptsPagedResponse = {
    total: number;
    pageNumber: number;
    pageSize: number;
    items: LicenseActivationAttemptListItemDto[];
};

/** Query for <c>/api/admin/licenses/export/*</c> and <c>/report/summary</c>. */
export type LicenseExportReportParams = {
    /** ISO-8601 UTC; filters <c>issued_licenses.issued_at_utc</c> and activation rows when set. */
    fromUtc?: string | null;
    toUtc?: string | null;
    includeActivationHistory?: boolean;
    maskLicenseKeys?: boolean;
};

export type LicenseReportSummaryDto = {
    generatedAtUtc: string;
    issuedTotalInDateFilter: number;
    issuedActiveEligible: number;
    issuedRevoked: number;
    issuedCancelled: number;
    issuedDeleted: number;
    expiringWithin30Days: number;
    expiringWithin15Days: number;
    expiringWithin7Days: number;
    uniqueActivatedDevices: number;
    activationAttemptsInDateFilter: number;
};

export const licenseQueryKeys = {
    status: ['admin', 'license', 'status'] as const,
    /** GET /api/license/status (anonymous, POS contract). */
    publicStatus: ['admin', 'license', 'publicStatus'] as const,
    listRoot: ['admin', 'license', 'list'] as const,
    list: (params: IssuedLicensesListParams) => [...licenseQueryKeys.listRoot, params] as const,
    activationAttemptsRoot: ['admin', 'license', 'activationAttempts'] as const,
    activationAttempts: (params: LicenseActivationAttemptsListParams) =>
        [...licenseQueryKeys.activationAttemptsRoot, params] as const,
    reportSummary: (params: LicenseExportReportParams) => ['admin', 'licenses', 'report', 'summary', params] as const,
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

export async function getLicenseActivationAttempts(
    params: LicenseActivationAttemptsListParams | undefined,
): Promise<LicenseActivationAttemptsPagedResponse> {
    const { data } = await AXIOS_INSTANCE.get<LicenseActivationAttemptsPagedResponse>(
        '/api/admin/license/activation-attempts',
        {
            params: {
                licenseKey: params?.licenseKey?.trim() ? params.licenseKey.trim() : undefined,
                fromUtc: params?.fromUtc?.trim() ? params.fromUtc.trim() : undefined,
                toUtc: params?.toUtc?.trim() ? params.toUtc.trim() : undefined,
                status: params?.status?.trim() ? params.status.trim() : undefined,
                machineFingerprint: params?.machineFingerprint?.trim()
                    ? params.machineFingerprint.trim()
                    : undefined,
                pageNumber: params?.pageNumber ?? 1,
                pageSize: params?.pageSize ?? 50,
            },
        },
    );
    return data;
}

export async function postForceDeactivateActivationAttempt(id: string): Promise<void> {
    await AXIOS_INSTANCE.post(`/api/admin/license/activation-attempts/${encodeURIComponent(id)}/force-deactivate`);
}

function licenseExportQueryParams(params: LicenseExportReportParams | undefined) {
    const p: Record<string, string | boolean | undefined> = {
        fromUtc: params?.fromUtc?.trim() ? params.fromUtc.trim() : undefined,
        toUtc: params?.toUtc?.trim() ? params.toUtc.trim() : undefined,
    };
    if (params && params.includeActivationHistory !== undefined) {
        p.includeActivationHistory = params.includeActivationHistory;
    }
    if (params && params.maskLicenseKeys !== undefined) {
        p.maskLicenseKeys = params.maskLicenseKeys;
    }
    return p;
}

export async function getLicenseReportSummary(
    params: LicenseExportReportParams | undefined,
): Promise<LicenseReportSummaryDto> {
    const { data } = await AXIOS_INSTANCE.get<LicenseReportSummaryDto>('/api/admin/licenses/report/summary', {
        params: licenseExportQueryParams(params),
    });
    return data;
}

export async function downloadLicenseExportFile(
    format: 'csv' | 'json',
    params: LicenseExportReportParams | undefined,
): Promise<Blob> {
    const path = format === 'csv' ? '/api/admin/licenses/export/csv' : '/api/admin/licenses/export/json';
    const { data } = await AXIOS_INSTANCE.get<Blob>(path, {
        params: licenseExportQueryParams({
            ...params,
            includeActivationHistory: params?.includeActivationHistory,
            maskLicenseKeys: params?.maskLicenseKeys,
        }),
        responseType: 'blob',
    });
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
            machineFingerprint: params?.machineFingerprint?.trim()
                ? params.machineFingerprint.trim()
                : undefined,
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

export type IssuedLicenseActivationDto = {
    machineFingerprint: string;
    activatedAtUtc: string;
    lastSeenAtUtc: string;
    validUntilUtc: string;
    customerName: string;
};

export type IssuedLicenseDetailResponse = {
    id: string;
    licenseKey: string;
    customerName: string;
    expiryAtUtc: string;
    requireFingerprint: boolean;
    machineHashHex: string | null;
    signedJwt: string;
    issuedAtUtc: string;
    issuedByUserId: string | null;
    isRevoked: boolean;
    revokedAtUtc: string | null;
    revocationReason: string | null;
    supersededByLicenseId: string | null;
    transferredToLicenseId: string | null;
    isCancelled: boolean;
    cancelledAtUtc: string | null;
    isDeleted: boolean;
    deletedAtUtc: string | null;
    activations: IssuedLicenseActivationDto[];
};

export type ExtendIssuedLicenseRequest = {
    addDays?: number;
    addMonths?: number;
};

export type RevokeIssuedLicenseByIdRequest = {
    reason?: string | null;
};

export type CancelIssuedLicenseRequest = {
    reason?: string | null;
};

export async function getIssuedLicenseDetail(issuedLicenseId: string): Promise<IssuedLicenseDetailResponse> {
    const { data } = await AXIOS_INSTANCE.get<IssuedLicenseDetailResponse>(
        `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/details`,
    );
    return data;
}

export async function postExtendIssuedLicense(
    issuedLicenseId: string,
    body: ExtendIssuedLicenseRequest,
): Promise<GenerateLicenseResponse> {
    const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
        `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/extend`,
        body,
    );
    return data;
}

export async function postRevokeIssuedLicenseById(
    issuedLicenseId: string,
    body?: RevokeIssuedLicenseByIdRequest,
): Promise<void> {
    await AXIOS_INSTANCE.post(`/api/admin/license/${encodeURIComponent(issuedLicenseId)}/revoke`, body ?? {});
}

export async function postCancelIssuedLicense(issuedLicenseId: string, body?: CancelIssuedLicenseRequest): Promise<void> {
    await AXIOS_INSTANCE.post(`/api/admin/license/${encodeURIComponent(issuedLicenseId)}/cancel`, body ?? {});
}

export async function deleteIssuedLicenseSoft(issuedLicenseId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/license/${encodeURIComponent(issuedLicenseId)}`);
}

export async function postUnregisterIssuedLicenseMachine(issuedLicenseId: string): Promise<GenerateLicenseResponse> {
    const { data } = await AXIOS_INSTANCE.post<GenerateLicenseResponse>(
        `/api/admin/license/${encodeURIComponent(issuedLicenseId)}/unregister-machine`,
    );
    return data;
}

export type LicenseDashboardSummaryDto = {
    activeLicenses: number;
    expiringWithin30Days: number;
    expiredLicenses: number;
    uniqueActivatedDevices: number;
};

export type LicenseActivationSeriesPointDto = {
    periodStartUtc: string;
    count: number;
};

export type LicenseDashboardActivationSeriesDto = {
    granularity: string;
    points: LicenseActivationSeriesPointDto[];
};

export type LicenseDashboardActivityRowDto = {
    timestampUtc: string;
    licenseKeyMasked: string;
    machineFingerprintShort: string | null;
    action: string;
    sourceCode: string;
};

export type LicenseDashboardRecentActivityDto = {
    items: LicenseDashboardActivityRowDto[];
};

export const licenseDashboardQueryKeys = {
    summary: ['admin', 'license', 'dashboard', 'summary'] as const,
    series: (granularity: string, lookbackDays: number) =>
        ['admin', 'license', 'dashboard', 'series', granularity, lookbackDays] as const,
    activity: (take: number) => ['admin', 'license', 'dashboard', 'activity', take] as const,
};

export async function getLicenseDashboardSummary(): Promise<LicenseDashboardSummaryDto> {
    const { data } = await AXIOS_INSTANCE.get<LicenseDashboardSummaryDto>('/api/admin/license/dashboard/summary');
    return data;
}

export async function getLicenseDashboardActivationSeries(params: {
    granularity: 'day' | 'week';
    lookbackDays?: number;
}): Promise<LicenseDashboardActivationSeriesDto> {
    const { data } = await AXIOS_INSTANCE.get<LicenseDashboardActivationSeriesDto>(
        '/api/admin/license/dashboard/activation-series',
        {
            params: {
                granularity: params.granularity,
                lookbackDays: params.lookbackDays ?? 30,
            },
        },
    );
    return data;
}

export async function getLicenseDashboardRecentActivity(take?: number): Promise<LicenseDashboardRecentActivityDto> {
    const { data } = await AXIOS_INSTANCE.get<LicenseDashboardRecentActivityDto>(
        '/api/admin/license/dashboard/recent-activity',
        {
            params: { take: take ?? 25 },
        },
    );
    return data;
}

/** Triggers CSV download in the browser (UTF-8 BOM from server). */
export async function downloadLicenseDashboardReportCsv(): Promise<void> {
    const res = await AXIOS_INSTANCE.get<Blob>('/api/admin/license/dashboard/report.csv', {
        responseType: 'blob',
    });
    const blob = res.data;
    const disposition = res.headers['content-disposition'] as string | undefined;
    let fileName = 'license-dashboard-report.csv';
    const match = disposition?.match(/filename\*?=(?:UTF-8'')?["']?([^"';]+)/i);
    if (match?.[1]) {
        fileName = decodeURIComponent(match[1].trim());
    }
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.rel = 'noopener';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
}
