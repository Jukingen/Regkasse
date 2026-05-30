import type { Dayjs } from 'dayjs';

/** Client-side payment list filters (mapped to GET /api/admin/payments query params). */
export interface PaymentFilters {
    dateRange?: [Dayjs, Dayjs] | null;
    paymentMethods?: string[];
    statuses?: string[];
    receiptNumber?: string;
    minAmount?: number;
    maxAmount?: number;
    cashRegisterId?: string;
    customerName?: string;
    customerEmail?: string;
    cashierId?: string;
    isStorno?: boolean;
    isRefund?: boolean;
}

export interface PaymentFilterSummary {
    activeFilterCount?: number;
    appliedFilters?: Record<string, unknown>;
    availablePaymentMethods?: string[];
    availableStatuses?: string[];
}
