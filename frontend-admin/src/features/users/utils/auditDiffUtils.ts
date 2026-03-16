/**
 * Audit diff parsing for user activity timeline.
 * Supports structured changes JSON [{ field, oldValue, newValue }] and legacy oldValues/newValues.
 *
 * Audit log invariants (UI):
 * - Invariant 4: Sensitive data must never be logged or displayed; only whitelisted keys are rendered.
 * - Invariant 5: Gracefully handle incomplete historical records (null, missing metadata, invalid JSON);
 *   never throw; return null or empty and use EMPTY_PLACEHOLDER for missing values.
 */

export const EMPTY_PLACEHOLDER = '—';
export const DIFF_CELL_MAX_LENGTH = 120;

/** Keys allowed for diff display. Includes all editable user fields (Steuernummer, Notizen, Mitarbeiternummer). Never render password, tokens, credentials, security stamps. */
export const ALLOWED_DIFF_KEYS = new Set(
    ['firstName', 'lastName', 'email', 'userName', 'role', 'isActive', 'isDemo', 'taxNumber', 'notes', 'employeeNumber',
        'FirstName', 'LastName', 'Email', 'UserName', 'Role', 'IsActive', 'IsDemo', 'TaxNumber', 'Notes', 'EmployeeNumber']
);

export type DiffRow = { field: string; label: string; oldVal: string; newVal: string };

export type StructuredChangeItem = { field?: string; oldValue?: unknown; newValue?: unknown };

export type DiffFormatOptions = {
    emptyPlaceholder: string;
    labelActive: string;
    labelInactive: string;
    maxCellLength: number;
};

const defaultFormatOptions: DiffFormatOptions = {
    emptyPlaceholder: EMPTY_PLACEHOLDER,
    labelActive: 'Aktiv',
    labelInactive: 'Inaktiv',
    maxCellLength: DIFF_CELL_MAX_LENGTH,
};

/** Returns true if the field is safe to display (whitelist). */
export function isAllowedDiffKey(key: string): boolean {
    if (!key || typeof key !== 'string') return false;
    const k = key.trim();
    return ALLOWED_DIFF_KEYS.has(k) || ALLOWED_DIFF_KEYS.has(k.charAt(0).toUpperCase() + k.slice(1));
}

/** User-friendly display for a single diff value. Never outputs raw sensitive data. */
export function formatDiffValue(
    key: string,
    value: unknown,
    options: Partial<DiffFormatOptions> = {}
): string {
    if (!isAllowedDiffKey(key)) return EMPTY_PLACEHOLDER;
    const opts = { ...defaultFormatOptions, ...options };
    if (value === null || value === undefined) return opts.emptyPlaceholder;
    if (value === '') return opts.emptyPlaceholder;
    if (typeof value === 'boolean') return value ? opts.labelActive : opts.labelInactive;
    const s = String(value);
    return s.length > opts.maxCellLength ? `${s.slice(0, opts.maxCellLength)}…` : s;
}

/**
 * Parses structured changes JSON (backend enterprise format).
 * Returns diff rows only for whitelisted keys. Safe for null/invalid JSON.
 */
export function parseStructuredChanges(
    changesJson: string | null | undefined,
    getLabel: (key: string) => string,
    formatOptions: Partial<DiffFormatOptions> = {}
): DiffRow[] | null {
    const str = changesJson != null && typeof changesJson === 'string' ? changesJson.trim() : '';
    if (!str) return null;
    let arr: unknown[];
    try {
        const parsed = JSON.parse(str);
        if (!Array.isArray(parsed)) return null;
        arr = parsed;
    } catch {
        return null;
    }
    const opts = { ...defaultFormatOptions, ...formatOptions };
    const rows: DiffRow[] = [];
    for (const item of arr) {
        if (item == null || typeof item !== 'object') continue;
        const o = item as StructuredChangeItem;
        const field = (o.field != null && typeof o.field === 'string') ? o.field.trim() : '';
        if (!field || !isAllowedDiffKey(field)) continue;
        rows.push({
            field,
            label: getLabel(field),
            oldVal: formatDiffValue(field, o.oldValue, opts),
            newVal: formatDiffValue(field, o.newValue, opts),
        });
    }
    return rows.length ? rows : null;
}

/**
 * Parses legacy OldValues/NewValues JSON into diff rows. Only whitelisted keys are included.
 * Returns null on parse error or when no diff. Does not throw.
 */
export function parseAuditDiff(
    oldValues: string | null | undefined,
    newValues: string | null | undefined,
    getLabel: (key: string) => string,
    formatOptions: Partial<DiffFormatOptions> = {}
): DiffRow[] | null {
    if (oldValues !== undefined && oldValues !== null && typeof oldValues !== 'string') return null;
    if (newValues !== undefined && newValues !== null && typeof newValues !== 'string') return null;
    const oldStr = typeof oldValues === 'string' ? oldValues.trim() : '';
    const newStr = typeof newValues === 'string' ? newValues.trim() : '';
    if (!oldStr && !newStr) return null;
    let oldObj: Record<string, unknown> = {};
    let newObj: Record<string, unknown> = {};
    try {
        if (oldStr) oldObj = JSON.parse(oldStr) as Record<string, unknown>;
        if (newStr) newObj = JSON.parse(newStr) as Record<string, unknown>;
    } catch {
        return null;
    }
    if (typeof oldObj !== 'object' || oldObj === null) oldObj = {};
    if (typeof newObj !== 'object' || newObj === null) newObj = {};
    const allKeys = Array.from(new Set([...Object.keys(oldObj), ...Object.keys(newObj)]));
    const rows: DiffRow[] = [];
    const opts = { ...defaultFormatOptions, ...formatOptions };
    for (const key of allKeys) {
        if (!isAllowedDiffKey(key)) continue;
        const o = oldObj[key];
        const n = newObj[key];
        if (o === n && (o !== undefined || n !== undefined)) continue;
        rows.push({
            field: key,
            label: getLabel(key),
            oldVal: formatDiffValue(key, o, opts),
            newVal: formatDiffValue(key, n, opts),
        });
    }
    return rows.length ? rows : null;
}

/**
 * Best-effort diff rows from an audit entry: prefers structured changes, falls back to oldValues/newValues.
 * Invariant 5: Safe for null/incomplete entry or invalid JSON; returns null instead of throwing.
 * Only whitelisted fields are included (invariant 4).
 */
export function getDiffRowsFromEntry(
    entry: {
        changes?: string | null;
        oldValues?: string | null;
        newValues?: string | null;
    },
    getLabel: (key: string) => string,
    formatOptions: Partial<DiffFormatOptions> = {}
): DiffRow[] | null {
    const fromStructured = parseStructuredChanges(entry.changes ?? undefined, getLabel, formatOptions);
    if (fromStructured && fromStructured.length > 0) return fromStructured;
    return parseAuditDiff(entry.oldValues, entry.newValues, getLabel, formatOptions);
}
