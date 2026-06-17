import dayjs from 'dayjs';
import type { PaymentFilters } from '@/features/payments/types/paymentFilters';

export type AdminPaymentsListQueryParams = {
    startDate?: string;
    endDate?: string;
    minAmount?: number;
    maxAmount?: number;
    paymentMethods?: string[];
    statuses?: string[];
    cashRegisterId?: string;
    customerName?: string;
    customerEmail?: string;
    cashierId?: string;
    receiptNumber?: string;
    isStorno?: boolean;
    isRefund?: boolean;
    page?: number;
    pageSize?: number;
    pageNumber?: number;
    sortBy?: string;
    sortDirection?: string;
    afterCursor?: string;
    includeTotalCount?: boolean;
};

export function paymentFiltersToApiParams(
    filters: PaymentFilters,
    pagination: { page: number; pageSize: number; afterCursor?: string; includeTotalCount?: boolean },
): AdminPaymentsListQueryParams {
    const start = filters.dateRange?.[0] ?? dayjs().subtract(30, 'day');
    const end = filters.dateRange?.[1] ?? dayjs();

    const params: AdminPaymentsListQueryParams = {
        startDate: start.format('YYYY-MM-DD'),
        endDate: end.format('YYYY-MM-DD'),
        page: pagination.page,
        pageNumber: pagination.page,
        pageSize: pagination.pageSize,
        sortBy: 'CreatedAt',
        sortDirection: 'desc',
        includeTotalCount: pagination.includeTotalCount ?? pagination.page === 1,
    };

    if (pagination.afterCursor) {
        params.afterCursor = pagination.afterCursor;
    }

    if (filters.minAmount != null && Number.isFinite(filters.minAmount)) {
        params.minAmount = filters.minAmount;
    }
    if (filters.maxAmount != null && Number.isFinite(filters.maxAmount)) {
        params.maxAmount = filters.maxAmount;
    }
    if (filters.paymentMethods && filters.paymentMethods.length > 0) {
        params.paymentMethods = filters.paymentMethods;
    }
    if (filters.statuses && filters.statuses.length > 0) {
        params.statuses = filters.statuses;
    }
    if (filters.cashRegisterId) {
        params.cashRegisterId = filters.cashRegisterId;
    }
    if (filters.customerName?.trim()) {
        params.customerName = filters.customerName.trim();
    }
    if (filters.customerEmail?.trim()) {
        params.customerEmail = filters.customerEmail.trim();
    }
    if (filters.cashierId) {
        params.cashierId = filters.cashierId;
    }
    if (filters.receiptNumber?.trim()) {
        params.receiptNumber = filters.receiptNumber.trim();
    }
    if (filters.isStorno === true) {
        params.isStorno = true;
    }
    if (filters.isRefund === true) {
        params.isRefund = true;
    }

    return params;
}
