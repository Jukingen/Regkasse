/**
 * RKSV Verifications admin surface: view logic over Orval `AuditLogEntryDto` only.
 * No verification-result DTO — this is audit-log triage with client keyword sampling.
 */

import type { AuditLogEntryDto } from '@/api/generated/model';
import { parseAuthoritativeRegisterGuid } from '@/shared/utils/registerIdentity';

/**
 * Client-side keyword window used on the Verifications page (not server contract).
 * Backend action renames can narrow matches without OpenAPI notice.
 */
export function auditLogMatchesVerificationsKeywordSample(e: AuditLogEntryDto): boolean {
    const action = (e.action ?? '').toLowerCase();
    const entity = (e.entityType ?? '').toLowerCase();
    return (
        action.includes('signature') ||
        action.includes('offline') ||
        entity.includes('receipt') ||
        entity.includes('payment') ||
        entity.includes('offlinetransaction')
    );
}

/** Backend `AuditLogStatus` enum ordinal order — see `KasseAPI_Final.Models.AuditLog.AuditLogStatus`. */
const AUDIT_LOG_STATUS_ORDINAL_LABELS = [
    'Success',
    'Failed',
    'Pending',
    'Cancelled',
    'InProgress',
    'Timeout',
    'ValidationError',
    'AuthorizationError',
    'SystemError',
    'Warning',
    'Error',
] as const;

export type AuditLogStatusPresentation = {
    label: string;
    antColor: 'success' | 'error' | 'warning' | 'processing' | 'default';
};

/**
 * Honest status display: handles numeric enums from JSON when OpenAPI maps enum incorrectly.
 */
export function viewAuditLogStatusPresentation(status: AuditLogEntryDto['status']): AuditLogStatusPresentation {
    if (status === undefined || status === null) {
        return { label: '—', antColor: 'default' };
    }
    if (typeof status === 'number' && Number.isInteger(status)) {
        const idx = status;
        const label =
            idx >= 0 && idx < AUDIT_LOG_STATUS_ORDINAL_LABELS.length
                ? AUDIT_LOG_STATUS_ORDINAL_LABELS[idx]
                : `Status(${idx})`;
        if (idx === 0) return { label, antColor: 'success' };
        if (idx === 1 || idx === 6 || idx === 7 || idx === 8 || idx === 10) return { label, antColor: 'error' };
        if (idx === 9) return { label, antColor: 'warning' };
        return { label, antColor: 'default' };
    }
    const s = String(status);
    if (s === 'Success') return { label: s, antColor: 'success' };
    if (
        s === 'Failed' ||
        s === 'ValidationError' ||
        s === 'AuthorizationError' ||
        s === 'SystemError' ||
        s === 'Error'
    ) {
        return { label: s, antColor: 'error' };
    }
    if (s === 'Warning') return { label: s, antColor: 'warning' };
    return { label: s, antColor: 'default' };
}

export type AuditLogEntityDeepLinks = {
    /** `/payments?paymentId=` when entity is Payment and entityId is link-safe UUID */
    paymentListHref?: string;
    /** `/receipts/{id}` when entity is Receipt and entityId is link-safe UUID */
    receiptDetailHref?: string;
};

/**
 * Deep links only when `entityType` + `entityId` match known fiscal entities and id passes the same UUID gate as register links.
 */
export function viewAuditLogEntityDeepLinks(row: AuditLogEntryDto): AuditLogEntityDeepLinks {
    const id = parseAuthoritativeRegisterGuid(row.entityId);
    if (!id) return {};
    const et = (row.entityType ?? '').trim().toLowerCase();
    if (et === 'payment') {
        return { paymentListHref: `/payments?paymentId=${encodeURIComponent(id)}` };
    }
    if (et === 'receipt') {
        return { receiptDetailHref: `/receipts/${encodeURIComponent(id)}` };
    }
    return {};
}
