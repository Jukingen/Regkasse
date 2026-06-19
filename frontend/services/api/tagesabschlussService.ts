import { apiClient } from './config';
import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';
import { formatUserDate, formatUserTime } from '../../utils/dateFormatter';

export interface DailyClosingRequest {
    cashRegisterId: string;
}

export interface TagesabschlussResult {
    success: boolean;
    errorMessage?: string;
    /** When closing is blocked: count of payments without a matching invoice (backend). */
    paymentsWithoutInvoiceCount?: number;
    closingId?: string;
    closingDate: string;
    closingType?: string;
    totalAmount: number;
    totalTaxAmount: number;
    transactionCount: number;
    tseSignature?: string;
    status?: string;
    finanzOnlineStatus?: string;
}

export interface ClosingHistoryItem {
    success: boolean;
    closingId: string;
    closingDate: string;
    closingType: string;
    totalAmount: number;
    totalTaxAmount: number;
    transactionCount: number;
    tseSignature: string;
    status: string;
    finanzOnlineStatus?: string;
}

export interface CanCloseResponse {
    canClose: boolean;
    lastClosingDate?: string;
    message: string;
    paymentsWithoutInvoiceCount?: number;
}

export interface ClosingStatistics {
    totalClosings: number;
    totalAmount: number;
    totalTaxAmount: number;
    totalTransactions: number;
    averageDailyAmount: number;
    lastClosingDate?: string;
}

function asClosingResult(raw: unknown): TagesabschlussResult {
    return unwrapApiResponseLayer(raw) as TagesabschlussResult;
}

function asClosingHistory(raw: unknown): ClosingHistoryItem[] {
    const layer = unwrapApiResponseLayer(raw);
    return Array.isArray(layer) ? (layer as ClosingHistoryItem[]) : [];
}

function asCanClose(raw: unknown): CanCloseResponse {
    return unwrapApiResponseLayer(raw) as CanCloseResponse;
}

function asStatistics(raw: unknown): ClosingStatistics {
    return unwrapApiResponseLayer(raw) as ClosingStatistics;
}

function readErrorPayload(error: unknown): { error?: string; paymentsWithoutInvoiceCount?: number } | null {
    const e = error as { data?: unknown; response?: { data?: unknown } } | null;
    const data = (e?.data ?? e?.response?.data) as Record<string, unknown> | null | undefined;
    if (!data || typeof data !== 'object') return null;
    return {
        error: typeof data.error === 'string' ? data.error : undefined,
        paymentsWithoutInvoiceCount:
            typeof data.paymentsWithoutInvoiceCount === 'number' ? data.paymentsWithoutInvoiceCount : undefined,
    };
}

/**
 * Perform daily closing for the current day
 */
export const performDailyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
    try {
        const raw = await apiClient.post<unknown>('/tagesabschluss/daily', request);
        return asClosingResult(raw);
    } catch (error: unknown) {
        console.error('Daily closing failed:', error);
        const data = readErrorPayload(error);
        return {
            success: false,
            errorMessage: data?.error || 'Daily closing failed',
            paymentsWithoutInvoiceCount: data?.paymentsWithoutInvoiceCount,
            closingDate: new Date().toISOString(),
            totalAmount: 0,
            totalTaxAmount: 0,
            transactionCount: 0,
        };
    }
};

/**
 * Perform monthly closing for the current month
 */
export const performMonthlyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
    try {
        const raw = await apiClient.post<unknown>('/tagesabschluss/monthly', request);
        return asClosingResult(raw);
    } catch (error: unknown) {
        console.error('Monthly closing failed:', error);
        const data = readErrorPayload(error);
        return {
            success: false,
            errorMessage: data?.error || 'Monthly closing failed',
            paymentsWithoutInvoiceCount: data?.paymentsWithoutInvoiceCount,
            closingDate: new Date().toISOString(),
            totalAmount: 0,
            totalTaxAmount: 0,
            transactionCount: 0,
        };
    }
};

/**
 * Perform yearly closing for the current year
 */
export const performYearlyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
    try {
        const raw = await apiClient.post<unknown>('/tagesabschluss/yearly', request);
        return asClosingResult(raw);
    } catch (error: unknown) {
        console.error('Yearly closing failed:', error);
        const data = readErrorPayload(error);
        return {
            success: false,
            errorMessage: data?.error || 'Yearly closing failed',
            paymentsWithoutInvoiceCount: data?.paymentsWithoutInvoiceCount,
            closingDate: new Date().toISOString(),
            totalAmount: 0,
            totalTaxAmount: 0,
            transactionCount: 0,
        };
    }
};

/**
 * Get closing history for the authenticated user
 */
export const getClosingHistory = async (
    fromDate?: string,
    toDate?: string,
    cashRegisterId?: string
): Promise<ClosingHistoryItem[]> => {
    try {
        const params = new URLSearchParams();
        if (fromDate) params.append('fromDate', fromDate);
        if (toDate) params.append('toDate', toDate);
        if (cashRegisterId) params.append('cashRegisterId', cashRegisterId);

        const query = params.toString();
        const url = query ? `/tagesabschluss/history?${query}` : '/tagesabschluss/history';
        const raw = await apiClient.get<unknown>(url);
        return asClosingHistory(raw);
    } catch (error: unknown) {
        console.error('Failed to get closing history:', error);
        return [];
    }
};

/**
 * Check if daily closing can be performed for a cash register
 */
export const canPerformClosing = async (cashRegisterId: string): Promise<CanCloseResponse> => {
    try {
        const raw = await apiClient.get<unknown>(`/tagesabschluss/can-close/${cashRegisterId}`);
        return asCanClose(raw);
    } catch (error: unknown) {
        console.error('Failed to check if closing can be performed:', error);
        return {
            canClose: false,
            message: 'Failed to check closing status',
            paymentsWithoutInvoiceCount: undefined,
        };
    }
};

/**
 * Get closing statistics for a specific period
 */
export const getClosingStatistics = async (
    fromDate?: string,
    toDate?: string,
    cashRegisterId?: string
): Promise<ClosingStatistics> => {
    try {
        const params = new URLSearchParams();
        if (fromDate) params.append('fromDate', fromDate);
        if (toDate) params.append('toDate', toDate);
        if (cashRegisterId) params.append('cashRegisterId', cashRegisterId);

        const query = params.toString();
        const url = query ? `/tagesabschluss/statistics?${query}` : '/tagesabschluss/statistics';
        const raw = await apiClient.get<unknown>(url);
        return asStatistics(raw);
    } catch (error: unknown) {
        console.error('Failed to get closing statistics:', error);
        return {
            totalClosings: 0,
            totalAmount: 0,
            totalTaxAmount: 0,
            totalTransactions: 0,
            averageDailyAmount: 0,
        };
    }
};

/** Format closing date for display (DD.MM.YYYY). */
export const formatClosingDate = (dateString: string): string => formatUserDate(dateString);

/** Format closing time for display (HH:mm:ss). */
export const formatClosingTime = (dateString: string): string =>
    formatUserTime(dateString, { includeSeconds: true });

/**
 * Get closing type display name
 */
export const getClosingTypeDisplayName = (closingType: string): string => {
    switch (closingType) {
        case 'Daily':
            return 'Tagesabschluss';
        case 'Monthly':
            return 'Monatsabschluss';
        case 'Yearly':
            return 'Jahresabschluss';
        default:
            return closingType;
    }
};

/**
 * Get closing status display name
 */
export const getClosingStatusDisplayName = (status: string): string => {
    switch (status) {
        case 'Completed':
            return 'Abgeschlossen';
        case 'Failed':
            return 'Fehlgeschlagen';
        case 'Pending':
            return 'Ausstehend';
        default:
            return status;
    }
};
