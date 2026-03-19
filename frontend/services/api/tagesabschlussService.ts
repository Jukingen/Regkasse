import { apiClient } from './config';

export interface DailyClosingRequest {
  cashRegisterId: string; // Keep as string for frontend, will be converted to Guid on backend
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
  /** Backend message (e.g. why closing is blocked or already performed). */
  message: string;
  /** Count of payments without invoice in scope; shown when canClose is false. */
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

/**
 * Perform daily closing for the current day
 */
export const performDailyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
  try {
    const response = await apiClient.post('/tagesabschluss/daily', request);
    return response.data;
  } catch (error: any) {
    console.error('Daily closing failed:', error);
    const data = error.response?.data;
    return {
      success: false,
      errorMessage: (typeof data?.error === 'string' ? data.error : null) || 'Daily closing failed',
      paymentsWithoutInvoiceCount: typeof data?.paymentsWithoutInvoiceCount === 'number' ? data.paymentsWithoutInvoiceCount : undefined,
      closingDate: new Date().toISOString(),
      totalAmount: 0,
      totalTaxAmount: 0,
      transactionCount: 0
    };
  }
};

/**
 * Perform monthly closing for the current month
 */
export const performMonthlyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
  try {
    const response = await apiClient.post('/tagesabschluss/monthly', request);
    return response.data;
  } catch (error: any) {
    console.error('Monthly closing failed:', error);
    const data = error.response?.data;
    return {
      success: false,
      errorMessage: (typeof data?.error === 'string' ? data.error : null) || 'Monthly closing failed',
      paymentsWithoutInvoiceCount: typeof data?.paymentsWithoutInvoiceCount === 'number' ? data.paymentsWithoutInvoiceCount : undefined,
      closingDate: new Date().toISOString(),
      totalAmount: 0,
      totalTaxAmount: 0,
      transactionCount: 0
    };
  }
};

/**
 * Perform yearly closing for the current year
 */
export const performYearlyClosing = async (request: DailyClosingRequest): Promise<TagesabschlussResult> => {
  try {
    const response = await apiClient.post('/tagesabschluss/yearly', request);
    return response.data;
  } catch (error: any) {
    console.error('Yearly closing failed:', error);
    const data = error.response?.data;
    return {
      success: false,
      errorMessage: (typeof data?.error === 'string' ? data.error : null) || 'Yearly closing failed',
      paymentsWithoutInvoiceCount: typeof data?.paymentsWithoutInvoiceCount === 'number' ? data.paymentsWithoutInvoiceCount : undefined,
      closingDate: new Date().toISOString(),
      totalAmount: 0,
      totalTaxAmount: 0,
      transactionCount: 0
    };
  }
};

/**
 * Get closing history for the authenticated user
 */
export const getClosingHistory = async (
  fromDate?: string,
  toDate?: string
): Promise<ClosingHistoryItem[]> => {
  try {
    const params = new URLSearchParams();
    if (fromDate) params.append('fromDate', fromDate);
    if (toDate) params.append('toDate', toDate);

    const response = await apiClient.get(`/tagesabschluss/history?${params.toString()}`);
    return response.data;
  } catch (error: any) {
    console.error('Failed to get closing history:', error);
    return [];
  }
};

/**
 * Check if daily closing can be performed for a cash register
 */
export const canPerformClosing = async (cashRegisterId: string): Promise<CanCloseResponse> => {
  try {
    const response = await apiClient.get(`/tagesabschluss/can-close/${cashRegisterId}`);
    return response.data;
  } catch (error: any) {
    console.error('Failed to check if closing can be performed:', error);
    return {
      canClose: false,
      message: 'Failed to check closing status',
      paymentsWithoutInvoiceCount: undefined
    };
  }
};

/**
 * Get closing statistics for a specific period
 */
export const getClosingStatistics = async (
  fromDate?: string,
  toDate?: string
): Promise<ClosingStatistics> => {
  try {
    const params = new URLSearchParams();
    if (fromDate) params.append('fromDate', fromDate);
    if (toDate) params.append('toDate', toDate);

    const response = await apiClient.get(`/tagesabschluss/statistics?${params.toString()}`);
    return response.data;
  } catch (error: any) {
    console.error('Failed to get closing statistics:', error);
    return {
      totalClosings: 0,
      totalAmount: 0,
      totalTaxAmount: 0,
      totalTransactions: 0,
      averageDailyAmount: 0
    };
  }
};

/**
 * Format closing date for display
 */
export const formatClosingDate = (dateString: string): string => {
  const date = new Date(dateString);
  return date.toLocaleDateString('de-DE', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  });
};

/**
 * Format closing time for display
 */
export const formatClosingTime = (dateString: string): string => {
  const date = new Date(dateString);
  return date.toLocaleTimeString('de-DE', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  });
};

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
