export const FA_QUICK_CASH_REGISTER_STORAGE_KEY = 'fa_quick_cash_register_id';

export const FA_QUICK_CASH_REGISTER_QUERY_PARAM = 'registerId';

export function readQuickCashRegisterId(): string | null {
    if (typeof window === 'undefined') {
        return null;
    }
    const value = window.sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)?.trim();
    return value || null;
}

export function writeQuickCashRegisterId(registerId: string | null): void {
    if (typeof window === 'undefined') {
        return;
    }
    if (!registerId) {
        window.sessionStorage.removeItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY);
        return;
    }
    window.sessionStorage.setItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY, registerId);
}
