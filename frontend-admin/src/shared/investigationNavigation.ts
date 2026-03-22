/**
 * Cross-screen RKSV operator investigation links.
 * Uses the same register UUID policy as {@link buildFinanzOnlineQueuePath}; never puts non-validated
 * register FKs into URLs. Extra query keys on the FO queue are UI-only (documented on that page).
 */

import { buildFinanzOnlineQueuePath, parseAuthoritativeRegisterGuid } from '@/shared/utils/registerIdentity';

const MAX_INVESTIGATION_CONTEXT_LEN = 256;

/** Truncate opaque context tokens for safe URL length. */
export function truncateInvestigationContextToken(raw: string): string {
    return raw.trim().slice(0, MAX_INVESTIGATION_CONTEXT_LEN);
}

export function buildIncidentInvestigationHref(correlationIdRaw: string): string {
    const t = correlationIdRaw.trim();
    if (!t) return '/rksv/incident';
    return `/rksv/incident?correlationId=${encodeURIComponent(t)}`;
}

export function buildReplayBatchDetailHref(correlationIdRaw: string): string {
    const t = correlationIdRaw.trim();
    if (!t) return '/rksv/replay-batch';
    return `/rksv/replay-batch/${encodeURIComponent(t)}`;
}

export function buildVerificationsAuditHref(correlationIdRaw: string): string {
    const t = correlationIdRaw.trim();
    if (!t) return '/rksv/verifications';
    return `/rksv/verifications?correlationId=${encodeURIComponent(t)}`;
}

/**
 * FinanzOnline reconciliation list path plus optional investigation hints.
 * `registerRowId`: API `cashRegisterId` string; only values passing `parseAuthoritativeRegisterGuid` appear as
 * query `cashRegisterId` (prefer passing `toLinkSafeRegisterRowId(apiFk)` from `registerIdentity` at call sites).
 * `focusPaymentId` is only accepted when it passes the same non-nil UUID check as register row ids.
 * `investigationBatchCorrelationId` is echoed for operator context — it does not filter the reconciliation API.
 */
export function buildFinanzOnlineQueueInvestigationHref(opts: {
    registerRowId?: string | null;
    focusPaymentId?: string | null;
    investigationBatchCorrelationId?: string | null;
    fromUtc?: string;
    toUtc?: string;
    statusCsv?: string;
}): string {
    const base = buildFinanzOnlineQueuePath({
        registerRowId: opts.registerRowId,
        fromUtc: opts.fromUtc,
        toUtc: opts.toUtc,
        statusCsv: opts.statusCsv,
    });
    const qIndex = base.indexOf('?');
    const path = qIndex >= 0 ? base.slice(0, qIndex) : base;
    const params = new URLSearchParams(qIndex >= 0 ? base.slice(qIndex + 1) : '');
    const pay = parseAuthoritativeRegisterGuid(opts.focusPaymentId);
    if (pay) params.set('focusPaymentId', pay);
    const ctx = opts.investigationBatchCorrelationId?.trim();
    if (ctx) params.set('investigationBatchCorrelationId', truncateInvestigationContextToken(ctx));
    return `${path}?${params.toString()}`;
}
