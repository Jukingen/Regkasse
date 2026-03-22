/**
 * Admin portal: cash_registers.Id (UUID) is the only authoritative register identity for
 * navigation, API filters, and FinanzOnline queue links. Values like kassenId / KassenID /
 * RegisterNumber are display-only and must never be substituted as FK fallbacks.
 */

const GUID_RE =
    /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/;

const DEFAULT_FINANZ_ONLINE_QUEUE_STATUS_CSV = 'Pending,Failed,NeedsReconciliation,Submitted';

/**
 * Normalizes a register row id from JSON/API. Returns undefined unless the value is a
 * non-empty, non-zero UUID string.
 */
export function parseAuthoritativeRegisterGuid(value?: string | null): string | undefined {
    const v = value?.trim();
    if (!v || v === '00000000-0000-0000-0000-000000000000') return undefined;
    return GUID_RE.test(v) ? v : undefined;
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
 * Builds `/rksv/finanz-online-queue` path with query params. Only validated UUIDs are sent as
 * `cashRegisterId` — display register numbers are ignored here by design.
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
