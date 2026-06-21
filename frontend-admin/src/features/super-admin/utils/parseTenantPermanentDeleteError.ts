import axios from 'axios';

import type { TenantPermanentDeleteErrorResponse } from '@/api/generated/model';

function isTenantPermanentDeleteErrorResponse(
    value: unknown,
): value is TenantPermanentDeleteErrorResponse {
    if (!value || typeof value !== 'object') return false;
    const body = value as Record<string, unknown>;
    return (
        typeof body.code === 'string' ||
        typeof body.message === 'string' ||
        (body.dependencies != null && typeof body.dependencies === 'object')
    );
}

/** Parses structured permanent-delete failures (400/403) from axios/orval errors. */
export function parseTenantPermanentDeleteError(
    error: unknown,
): TenantPermanentDeleteErrorResponse | null {
    if (!axios.isAxiosError(error)) return null;
    const data = error.response?.data;
    return isTenantPermanentDeleteErrorResponse(data) ? data : null;
}

export class TenantPermanentDeleteBlockedError extends Error {
    readonly response: TenantPermanentDeleteErrorResponse;

    constructor(response: TenantPermanentDeleteErrorResponse) {
        super(response.message?.trim() || 'Permanent delete blocked');
        this.name = 'TenantPermanentDeleteBlockedError';
        this.response = response;
    }
}
