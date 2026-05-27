/**
 * Reads a single property from audit oldValues/newValues/metadata JSON blobs.
 */

export function parseAuditJsonField(
    json: string | null | undefined,
    field: string,
): string | null {
    const raw = json?.trim();
    if (!raw) return null;
    try {
        const parsed = JSON.parse(raw) as Record<string, unknown>;
        if (parsed == null || typeof parsed !== 'object') return null;
        const value = parsed[field];
        if (value == null) return null;
        const text = String(value).trim();
        return text.length > 0 ? text : null;
    } catch {
        return null;
    }
}

export function parseAuditReason(record: {
    metadata?: string | null;
    notes?: string | null;
}): string | null {
    const fromMeta = parseAuditJsonField(record.metadata, 'reason');
    if (fromMeta) return fromMeta;
    const notes = record.notes?.trim();
    return notes && notes.length > 0 ? notes : null;
}
