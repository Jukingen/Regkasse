export function maskTenantLicenseKey(key: string | null | undefined): string {
    if (!key?.trim()) return '—';
    const trimmed = key.trim();
    if (trimmed.length <= 12) return trimmed;
    return `${trimmed.slice(0, 8)}…${trimmed.slice(-4)}`;
}

export function computeExtendedValidUntilUtc(
    currentValidUntilUtc: string | null | undefined,
    extendDays: number,
    now = Date.now(),
): string {
    const currentEnd = currentValidUntilUtc ? Date.parse(currentValidUntilUtc) : now;
    const baseMs = Math.max(now, Number.isNaN(currentEnd) ? now : currentEnd);
    const end = new Date(baseMs);
    end.setUTCDate(end.getUTCDate() + extendDays);
    end.setUTCHours(23, 59, 59, 0);
    return end.toISOString();
}
