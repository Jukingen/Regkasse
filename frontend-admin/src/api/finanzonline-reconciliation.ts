/**
 * FinanzOnline Reconciliation API — list and retry FO submission state.
 * Backend: GET/POST /api/admin/finanzonline-reconciliation (not in Orval spec).
 */

import { customInstance } from '@/lib/axios';

// ---------------------------------------------------------------------------
// Types (aligned with backend DTOs)
// ---------------------------------------------------------------------------

export type FinanzOnlineStatus =
    | 'Pending'
    | 'Failed'
    | 'NeedsReconciliation'
    | 'Submitted'
    | 'NotSent';

export interface FinanzOnlineReconciliationItemDto {
    paymentId: string;
    receiptNumber: string;
    createdAt: string;
    totalAmount: number;
    cashRegisterId: string;
    finanzOnlineStatus: string | null;
    finanzOnlineError: string | null;
    finanzOnlineReferenceId: string | null;
    finanzOnlineLastAttemptAtUtc: string | null;
    finanzOnlineRetryCount: number;
}

export interface FinanzOnlineReconciliationListResponse {
    total: number;
    items: FinanzOnlineReconciliationItemDto[];
}

export interface FinanzOnlineRetryResponse {
    success: boolean;
    message: string;
    referenceId: string | null;
    failureKind: string;
    submittedAt: string;
}

export interface FinanzOnlineMetricsResponse {
    submitTotal: number;
    submitFailedTotal: number;
    submitFailedTransient: number;
    submitFailedPermanent: number;
    submitFailedUnknown: number;
}

export interface GetReconciliationListParams {
    status?: string; // comma-separated: Pending, Failed, NeedsReconciliation, Submitted
    cashRegisterId?: string;
    fromUtc?: string; // ISO
    toUtc?: string;
    limit?: number;
}

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

const BASE = '/api/admin/finanzonline-reconciliation';

export async function getReconciliationList(
    params: GetReconciliationListParams = {}
): Promise<FinanzOnlineReconciliationListResponse> {
    const search = new URLSearchParams();
    if (params.status) search.set('status', params.status);
    if (params.cashRegisterId) search.set('cashRegisterId', params.cashRegisterId);
    if (params.fromUtc) search.set('fromUtc', params.fromUtc);
    if (params.toUtc) search.set('toUtc', params.toUtc);
    if (params.limit != null) search.set('limit', String(params.limit));
    const qs = search.toString();
    return customInstance<FinanzOnlineReconciliationListResponse>({
        url: qs ? `${BASE}?${qs}` : BASE,
        method: 'GET',
    });
}

export async function retryReconciliationSubmit(
    paymentId: string
): Promise<FinanzOnlineRetryResponse> {
    return customInstance<FinanzOnlineRetryResponse>({
        url: `${BASE}/retry/${paymentId}`,
        method: 'POST',
    });
}

export async function getReconciliationMetrics(): Promise<FinanzOnlineMetricsResponse> {
    return customInstance<FinanzOnlineMetricsResponse>({
        url: `${BASE}/metrics`,
        method: 'GET',
    });
}
