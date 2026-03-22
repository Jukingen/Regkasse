/**
 * FinanzOnline reconciliation list — contract boundaries (Orval DTO only).
 * Surfaces what the API exposes vs. gaps that need OpenAPI/backend work.
 */

import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';

/** Row-level technical text from the server (no separate HTTP/body field on this DTO). */
export function finanzOnlineRowTechnicalResponseSummary(
    row: FinanzOnlineReconciliationItemDto,
): string | undefined {
    const t = row.finanzOnlineError?.trim();
    return t || undefined;
}

export type FinanzOnlineReconciliationContractGapKey =
    | 'correlationId'
    | 'errorClassPerRow'
    | 'environment'
    | 'lastSuccessLastFailure'
    | 'rawHttpPayload'
    | 'retryableFlag';

/** Keys missing from FinanzOnlineReconciliationItemDto — for tests and honest UI lists. */
export const FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS: readonly FinanzOnlineReconciliationContractGapKey[] =
    [
        'correlationId',
        'errorClassPerRow',
        'environment',
        'lastSuccessLastFailure',
        'rawHttpPayload',
        'retryableFlag',
    ] as const;
