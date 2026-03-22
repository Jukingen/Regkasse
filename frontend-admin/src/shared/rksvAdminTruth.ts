/**
 * Contract-first view helpers for RKSV / invoice / reconciliation admin surfaces.
 * Inputs are Orval DTO shapes only. Outputs separate raw API strings from derived link eligibility
 * (UUID validation for register-scoped URLs and filters) without inventing persisted fields.
 */

import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
import type { ReplayBatchDetailResponse } from '@/api/generated/model/replayBatchDetailResponse';
import {
    analyzeRegisterFkField,
    formatRegisterDisplayLabel,
} from '@/shared/utils/registerIdentity';

/** Documented backend/OpenAPI gaps — do not simulate these in the UI as persisted truth. */
export const RKSv_ADMIN_CONTRACT_GAPS = {
    invoiceDetailInvoiceItems:
        'Replace Invoice.invoiceItems `unknown` with a proper array DTO (or oneOf string vs array) in OpenAPI so Orval emits typed line items.',
    invoiceListSortByEnum:
        'Constrain GET /api/Invoice/list sortBy (and sortDir) with an enum in OpenAPI to match INVOICE_LIST_SORT_FIELDS in frontend-admin.',
    invoiceListRowOrigin:
        'Add listRowOrigin (e.g. PersistedInvoice | PaymentDerivedListRow) to InvoiceListItemDto in OpenAPI.',
    invoiceDetailProvenance:
        'Expose invoice row provenance (persisted vs payment-derived) on Invoice detail DTO if operators need it.',
    receiptListRegisterDisplay:
        'Add optional display-only register label (RegisterNumber / RKSV text) to ReceiptListItemDto, distinct from cashRegisterId.',
    replayBatchPaymentRegisterFk:
        'Add optional cashRegisterId (or linked FinanzOnline row id) on ReplayBatchPaymentItemDto so register is not inferred only via FO join.',
    finanzReconciliationRegisterDisplay:
        'Optional register display label on FinanzOnlineReconciliationItemDto alongside cashRegisterId UUID.',
} as const;

export type InvoiceListRegisterView = {
    /** Trimmed `cashRegisterId` from list DTO when non-empty (always show; may be non-UUID). */
    apiCashRegisterId: string | undefined;
    /** Display-only kassen id from the list DTO (not a machine FK). */
    kassenDisplay: string;
    /** UUID accepted for FO queue deep links / strict filters; undefined if absent or not link-safe. */
    finanzQueueRegisterRowId: string | undefined;
    /** API sent `cashRegisterId` but it is not link-safe (shape, nil UUID, etc.). */
    registerFkRawNotLinkSafe: boolean;
};

export function viewInvoiceListRegister(row: InvoiceListItemDto): InvoiceListRegisterView {
    const reg = analyzeRegisterFkField(row.cashRegisterId);
    return {
        apiCashRegisterId: reg.rawTrimmed,
        kassenDisplay: formatRegisterDisplayLabel(row.kassenId),
        finanzQueueRegisterRowId: reg.linkSafeUuid,
        registerFkRawNotLinkSafe: reg.isRawPresentButNotLinkSafe,
    };
}

export function viewFinanzReconciliationRegister(row: FinanzOnlineReconciliationItemDto): {
    apiCashRegisterId: string | undefined;
    finanzQueueRegisterRowId: string | undefined;
    registerFkRawNotLinkSafe: boolean;
} {
    const reg = analyzeRegisterFkField(row.cashRegisterId);
    return {
        apiCashRegisterId: reg.rawTrimmed,
        finanzQueueRegisterRowId: reg.linkSafeUuid,
        registerFkRawNotLinkSafe: reg.isRawPresentButNotLinkSafe,
    };
}

/**
 * Replay batch detail: correlation id is present on DTO; audit id is optional.
 * @param verificationsAuditOnly When true (replay detail page legacy behavior), Verifications link is emitted only if
 *        `auditCorrelationId` is set. When false, falls back to batch `correlationId` (incident aggregate pattern).
 */
export function viewReplayBatchTraceIds(
    batch: ReplayBatchDetailResponse,
    options?: { verificationsAuditOnly?: boolean },
): {
    batchCorrelationId: string | undefined;
    auditCorrelationId: string | null | undefined;
    incidentDeepLink: string | undefined;
    verificationsDeepLink: string | undefined;
} {
    const batchCorrelationId =
        batch.correlationId != null && String(batch.correlationId).trim() !== ''
            ? String(batch.correlationId)
            : undefined;
    const auditCorrelationId = batch.auditCorrelationId;
    const auditTrimmed =
        auditCorrelationId != null && String(auditCorrelationId).trim() !== ''
            ? String(auditCorrelationId)
            : undefined;
    const verificationsCorrelation = options?.verificationsAuditOnly
        ? auditTrimmed
        : auditTrimmed ?? batchCorrelationId;
    return {
        batchCorrelationId,
        auditCorrelationId,
        incidentDeepLink: batchCorrelationId
            ? `/rksv/incident?correlationId=${encodeURIComponent(batchCorrelationId)}`
            : undefined,
        verificationsDeepLink: verificationsCorrelation
            ? `/rksv/verifications?correlationId=${encodeURIComponent(verificationsCorrelation)}`
            : undefined,
    };
}
