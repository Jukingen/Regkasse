/**
 * Decode JWT payload (no signature verification — display/claim hints only).
 */

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
    const trimmed = token.trim();
    const normalized = trimmed.toLowerCase().startsWith('bearer ') ? trimmed.slice(7).trim() : trimmed;
    const parts = normalized.split('.');
    if (parts.length !== 3 || !parts[1]) {
        return null;
    }
    try {
        let b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const pad = b64.length % 4;
        if (pad) {
            b64 += '='.repeat(4 - pad);
        }
        const json = atob(b64);
        const parsed = JSON.parse(json) as unknown;
        return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : null;
    } catch {
        return null;
    }
}

export function isTruthyJwtClaim(value: unknown): boolean {
    return value === true || value === 'true' || value === 1 || value === '1';
}
