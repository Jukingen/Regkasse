/**
 * Register identity policy (frontend-admin)
 *
 * - **Backend FK field** (`cashRegisterId` on invoices/receipts/FO rows, etc.): show the trimmed
 *   string whenever the API sends it — even if it is not UUID-shaped — so operators never lose
 *   server truth to client parsing.
 * - **Link-safe UUID** (`parseAuthoritativeRegisterGuid` / `linkSafeUuid`): strict RFC 4122-style
 *   pattern + rejection of the all-zero UUID. Used only for: FinanzOnline queue deep-links,
 *   optional strict query params, and filters where sending a display register number would mislead.
 * - **Display-only** (`kassenId`, `registerNumber`, receipt `kassenID` text): never substituted as
 *   canonical FK and never passed to `buildFinanzOnlineQueuePath` as the only identifier.
 *
 * Prefer omitting or disabling a link over building one from a display label or a non-validated FK string.
 */

const GUID_RE =
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/;

const DEFAULT_FINANZ_ONLINE_QUEUE_STATUS_CSV = 'Pending,Failed,NeedsReconciliation,Submitted';

export const NIL_REGISTER_UUID = '00000000-0000-0000-0000-000000000000';

export type RegisterFkFieldAnalysis = {
    /** Trimmed API value when non-empty; use for display/copy. */
    rawTrimmed: string | undefined;
    /** Subset of raw values safe for deep-links and strict register filters (non-nil UUID). */
    linkSafeUuid: string | undefined;
    /** True when the API sent a non-empty value that is not link-safe (wrong shape, nil UUID, etc.). */
    isRawPresentButNotLinkSafe: boolean;
};

/**
 * Separates visible backend register FK text from UUIDs approved for links and strict filters.
 */
export function analyzeRegisterFkField(value?: string | null): RegisterFkFieldAnalysis {
    const t = value?.trim();
    if (!t) {
        return { rawTrimmed: undefined, linkSafeUuid: undefined, isRawPresentButNotLinkSafe: false };
    }
    const linkSafeUuid = parseAuthoritativeRegisterGuid(t);
    return {
        rawTrimmed: t,
        linkSafeUuid,
        isRawPresentButNotLinkSafe: !linkSafeUuid,
    };
}

/**
 * Normalizes a register row id for **links and strict machine filters** only. Returns undefined unless
 * the value is a non-empty, non-zero UUID string matching `GUID_RE`. Does not negate the existence of
 * `rawTrimmed` from {@link analyzeRegisterFkField} when this returns undefined.
 */
export function parseAuthoritativeRegisterGuid(value?: string | null): string | undefined {
    const v = value?.trim();
    if (!v || v === NIL_REGISTER_UUID) return undefined;
    return GUID_RE.test(v) ? v : undefined;
}

/**
 * Same strict non-nil UUID rule as {@link parseAuthoritativeRegisterGuid}, for payment row ids in URLs
 * (e.g. `focusPaymentId`). Never use display labels or receipt numbers here.
 */
export function parseAuthoritativePaymentGuid(value?: string | null): string | undefined {
    return parseAuthoritativeRegisterGuid(value);
}

/**
 * Pass this as `registerRowId` into {@link buildFinanzOnlineQueuePath} and
 * {@link buildFinanzOnlineQueueInvestigationHref} at call sites that hold an API `cashRegisterId` string.
 * Never pass `kassenId`, `kassenID`, or `registerNumber` here. Equivalent to {@link parseAuthoritativeRegisterGuid}
 * but names the intent: URL builders must not receive display identifiers as if they were authoritative FKs.
 */
export function toLinkSafeRegisterRowId(apiCashRegisterFkField?: string | null): string | undefined {
    return parseAuthoritativeRegisterGuid(apiCashRegisterFkField);
}

/** True when no usable FK exists for register-scoped navigation or machine filters. */
export function isMissingAuthoritativeRegisterId(value?: string | null): boolean {
    return !parseAuthoritativeRegisterGuid(value);
}

/**
 * Display-only register label (RegisterNumber / RKSV Kassen-ID text). For tables and copy;
 * do not pass to URL builders or API register filters.
 */
export function formatRegisterDisplayLabel(value?: string | null): string {
    const t = value?.trim();
    return t || '—';
}

/**
 * Optional display string for list DTOs: undefined when empty (keeps JSON lean).
 */
export function normalizeRegisterDisplayLabel(value?: string | null): string | undefined {
    const t = value?.trim();
    return t || undefined;
}

/**
 * Builds `/rksv/finanz-online-queue` path with query params.
 * `cashRegisterId` is set only when {@link parseAuthoritativeRegisterGuid} accepts the value — never
 * from display register numbers or ambiguous strings.
 */
export function buildFinanzOnlineQueuePath(options: {
    registerRowId?: string | null;
    fromUtc?: string;
    toUtc?: string;
    statusCsv?: string;
}): string {
    const params = new URLSearchParams();
    params.set('status', options.statusCsv ?? DEFAULT_FINANZ_ONLINE_QUEUE_STATUS_CSV);
    const fk = parseAuthoritativeRegisterGuid(options.registerRowId);
    if (fk) params.set('cashRegisterId', fk);
    if (options.fromUtc) params.set('fromUtc', options.fromUtc);
    if (options.toUtc) params.set('toUtc', options.toUtc);
    return `/rksv/finanz-online-queue?${params.toString()}`;
}
