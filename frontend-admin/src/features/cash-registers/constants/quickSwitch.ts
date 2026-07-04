export const FA_QUICK_CASH_REGISTER_STORAGE_KEY = 'fa_quick_cash_register_id';

export const FA_QUICK_CASH_REGISTER_QUERY_PARAM = 'registerId';

function scopedStorageKey(tenantId?: string | null): string | null {
    const normalized = tenantId?.trim();
    return normalized ? `${FA_QUICK_CASH_REGISTER_STORAGE_KEY}:${normalized}` : null;
}

/**
 * Reads the session-scoped quick-switch register id.
 * When `tenantId` is provided, uses a tenant-scoped key and migrates a legacy global value once.
 */
export function readQuickCashRegisterId(tenantId?: string | null): string | null {
    if (typeof window === 'undefined') {
        return null;
    }

    const scopedKey = scopedStorageKey(tenantId);
    if (scopedKey) {
        const scopedValue = window.sessionStorage.getItem(scopedKey)?.trim();
        if (scopedValue) {
            return scopedValue;
        }

        const legacyValue = window.sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)?.trim();
        if (legacyValue) {
            window.sessionStorage.setItem(scopedKey, legacyValue);
            window.sessionStorage.removeItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY);
            return legacyValue;
        }

        return null;
    }

    const value = window.sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)?.trim();
    return value || null;
}

/**
 * Persists the quick-switch register id for the current browser session.
 * Pass `tenantId` for Manager/tenant-scoped pages to avoid cross-tenant leakage.
 */
export function writeQuickCashRegisterId(
    registerId: string | null,
    tenantId?: string | null,
): void {
    if (typeof window === 'undefined') {
        return;
    }

    const scopedKey = scopedStorageKey(tenantId);
    if (scopedKey) {
        if (!registerId) {
            window.sessionStorage.removeItem(scopedKey);
            return;
        }
        window.sessionStorage.setItem(scopedKey, registerId);
        return;
    }

    if (!registerId) {
        window.sessionStorage.removeItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY);
        return;
    }
    window.sessionStorage.setItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY, registerId);
}
