import dayjs from 'dayjs';
import type { PaymentFilters } from '@/features/payments/types/paymentFilters';

const FILTER_PARAM_KEYS = [
    'startDate',
    'endDate',
    'paymentMethods',
    'statuses',
    'receiptNumber',
    'minAmount',
    'maxAmount',
    'cashRegisterId',
    'customerName',
    'customerEmail',
    'cashierId',
    'isStorno',
    'isRefund',
    'page',
    'pageSize',
] as const;

export function createDefaultPaymentFilters(): PaymentFilters {
    return {
        dateRange: [dayjs().subtract(30, 'day'), dayjs()],
    };
}

export function parsePaymentFiltersFromSearchParams(searchParams: URLSearchParams): PaymentFilters {
    const filters: PaymentFilters = createDefaultPaymentFilters();

    const startDate = searchParams.get('startDate');
    const endDate = searchParams.get('endDate');
    if (startDate && endDate && dayjs(startDate).isValid() && dayjs(endDate).isValid()) {
        filters.dateRange = [dayjs(startDate), dayjs(endDate)];
    }

    const methods = searchParams.get('paymentMethods');
    if (methods) {
        filters.paymentMethods = methods.split(',').map((m) => m.trim()).filter(Boolean);
    }

    const statuses = searchParams.get('statuses');
    if (statuses) {
        filters.statuses = statuses.split(',').map((s) => s.trim()).filter(Boolean);
    }

    const receiptNumber = searchParams.get('receiptNumber');
    if (receiptNumber) filters.receiptNumber = receiptNumber;

    const minAmount = searchParams.get('minAmount');
    if (minAmount != null && minAmount !== '' && Number.isFinite(Number(minAmount))) {
        filters.minAmount = Number(minAmount);
    }

    const maxAmount = searchParams.get('maxAmount');
    if (maxAmount != null && maxAmount !== '' && Number.isFinite(Number(maxAmount))) {
        filters.maxAmount = Number(maxAmount);
    }

    const cashRegisterId = searchParams.get('cashRegisterId');
    if (cashRegisterId) filters.cashRegisterId = cashRegisterId;

    const customerName = searchParams.get('customerName');
    if (customerName) filters.customerName = customerName;

    const customerEmail = searchParams.get('customerEmail');
    if (customerEmail) filters.customerEmail = customerEmail;

    const cashierId = searchParams.get('cashierId');
    if (cashierId) filters.cashierId = cashierId;

    if (searchParams.get('isStorno') === 'true') filters.isStorno = true;
    if (searchParams.get('isRefund') === 'true') filters.isRefund = true;

    return filters;
}

export function parsePaymentPaginationFromSearchParams(searchParams: URLSearchParams): {
    page: number;
    pageSize: number;
} {
    const pageRaw = searchParams.get('page');
    const pageSizeRaw = searchParams.get('pageSize');
    const page = pageRaw && Number.isFinite(Number(pageRaw)) ? Math.max(1, Number(pageRaw)) : 1;
    const pageSize =
        pageSizeRaw && Number.isFinite(Number(pageSizeRaw))
            ? Math.min(500, Math.max(1, Number(pageSizeRaw)))
            : 50;
    return { page, pageSize };
}

export function buildPaymentListSearchParams(
    filters: PaymentFilters,
    pagination: { page: number; pageSize: number },
    existing: URLSearchParams,
): URLSearchParams {
    const next = new URLSearchParams(existing.toString());

    for (const key of FILTER_PARAM_KEYS) {
        next.delete(key);
    }

    const start = filters.dateRange?.[0];
    const end = filters.dateRange?.[1];
    if (start && end) {
        next.set('startDate', start.format('YYYY-MM-DD'));
        next.set('endDate', end.format('YYYY-MM-DD'));
    }

    if (filters.paymentMethods && filters.paymentMethods.length > 0) {
        next.set('paymentMethods', filters.paymentMethods.join(','));
    }
    if (filters.statuses && filters.statuses.length > 0) {
        next.set('statuses', filters.statuses.join(','));
    }
    if (filters.receiptNumber?.trim()) next.set('receiptNumber', filters.receiptNumber.trim());
    if (filters.minAmount != null && Number.isFinite(filters.minAmount)) {
        next.set('minAmount', String(filters.minAmount));
    }
    if (filters.maxAmount != null && Number.isFinite(filters.maxAmount)) {
        next.set('maxAmount', String(filters.maxAmount));
    }
    if (filters.cashRegisterId) next.set('cashRegisterId', filters.cashRegisterId);
    if (filters.customerName?.trim()) next.set('customerName', filters.customerName.trim());
    if (filters.customerEmail?.trim()) next.set('customerEmail', filters.customerEmail.trim());
    if (filters.cashierId) next.set('cashierId', filters.cashierId);
    if (filters.isStorno === true) next.set('isStorno', 'true');
    if (filters.isRefund === true) next.set('isRefund', 'true');

    if (pagination.page > 1) next.set('page', String(pagination.page));
    if (pagination.pageSize !== 50) next.set('pageSize', String(pagination.pageSize));

    return next;
}
