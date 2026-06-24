import type { LicenseSaleResponse } from '@/api/generated/model';
import { LICENSE_SALE_PLAN_VALUES } from '@/features/billing/constants/licensePlans';

export function formatLicensePlanLabel(
    plan: string | null | undefined,
    t: (key: string) => string,
): string {
    switch (plan) {
        case LICENSE_SALE_PLAN_VALUES.sixMonths:
            return t('billing.plans.sixMonths');
        case LICENSE_SALE_PLAN_VALUES.twelveMonths:
            return t('billing.plans.twelveMonths');
        case LICENSE_SALE_PLAN_VALUES.custom:
            return t('billing.plans.custom');
        default:
            return plan ?? '—';
    }
}

export function formatSaleStatusLabel(
    status: string | null | undefined,
    t: (key: string) => string,
): string {
    switch (status) {
        case 'active':
            return t('billing.status.active');
        case 'cancelled':
            return t('billing.status.cancelled');
        case 'refunded':
            return t('billing.status.refunded');
        case 'expired':
            return t('billing.status.expired');
        default:
            return status ?? '—';
    }
}

export function isSaleCancellable(sale: Pick<LicenseSaleResponse, 'status'>): boolean {
    return sale.status === 'active';
}
