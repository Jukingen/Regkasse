import type { TenantDeleteDependencyCountsDto } from '@/api/generated/model';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';

export type TenantDeleteCountKey = keyof TenantDeleteDependencyCountsDto;

export type TenantDeleteCountRow = {
    key: TenantDeleteCountKey;
    labelKey: string;
    buildHref?: (tenantId: string) => string;
};

export const TENANT_DELETE_COUNT_ROWS: TenantDeleteCountRow[] = [
    {
        key: 'users',
        labelKey: 'tenants.deleteDependencies.counts.users',
        buildHref: (tenantId) => buildAdminUsersPageHref(tenantId),
    },
    {
        key: 'memberships',
        labelKey: 'tenants.deleteDependencies.counts.memberships',
        buildHref: (tenantId) => buildAdminUsersPageHref(tenantId),
    },
    {
        key: 'cashRegisters',
        labelKey: 'tenants.deleteDependencies.counts.cashRegisters',
        buildHref: (tenantId) => `/admin/tenants/${tenantId}?tab=registers`,
    },
    {
        key: 'payments',
        labelKey: 'tenants.deleteDependencies.counts.payments',
        buildHref: () => '/payments',
    },
    {
        key: 'receipts',
        labelKey: 'tenants.deleteDependencies.counts.receipts',
        buildHref: () => '/payments',
    },
    {
        key: 'dailyClosings',
        labelKey: 'tenants.deleteDependencies.counts.dailyClosings',
        buildHref: () => '/reporting/compliance',
    },
    {
        key: 'vouchers',
        labelKey: 'tenants.deleteDependencies.counts.vouchers',
        buildHref: () => '/vouchers',
    },
    {
        key: 'voucherLedgerEntries',
        labelKey: 'tenants.deleteDependencies.counts.voucherLedgerEntries',
        buildHref: () => '/vouchers',
    },
    {
        key: 'products',
        labelKey: 'tenants.deleteDependencies.counts.products',
        buildHref: () => '/products',
    },
    {
        key: 'categories',
        labelKey: 'tenants.deleteDependencies.counts.categories',
        buildHref: () => '/categories',
    },
    {
        key: 'finanzOnlineSubmissions',
        labelKey: 'tenants.deleteDependencies.counts.finanzOnlineSubmissions',
        buildHref: () => '/rksv/finanz-online-outbox',
    },
    {
        key: 'auditLogs',
        labelKey: 'tenants.deleteDependencies.counts.auditLogs',
        buildHref: (tenantId) => {
            const qp = new URLSearchParams({ entityType: 'Tenant', entityId: tenantId });
            return `/audit-logs?${qp.toString()}`;
        },
    },
];

export const TENANT_DELETE_FAILURE_CODE_KEYS: Record<string, string> = {
    tenant_not_found: 'tenants.deleteDependencies.failureCodes.tenantNotFound',
    legacy_default_tenant: 'tenants.deleteDependencies.failureCodes.legacyDefaultTenant',
    production_policy: 'tenants.deleteDependencies.failureCodes.productionPolicy',
    tenant_not_soft_deleted: 'tenants.deleteDependencies.failureCodes.notSoftDeleted',
    confirm_slug_mismatch: 'tenants.deleteDependencies.failureCodes.confirmSlugMismatch',
    cash_registers_present: 'tenants.deleteDependencies.failureCodes.cashRegistersPresent',
    fiscal_footprint_present: 'tenants.deleteDependencies.failureCodes.fiscalFootprintPresent',
    force_delete_development_only: 'tenants.deleteDependencies.failureCodes.forceDeleteDevelopmentOnly',
    remaining_dependencies: 'tenants.deleteDependencies.failureCodes.remainingDependencies',
};

export const TENANT_DELETE_BLOCKER_CODE_KEYS: Record<string, string> = {
    ...TENANT_DELETE_FAILURE_CODE_KEYS,
    memberships: 'tenants.deleteDependencies.blockers.memberships',
    catalog: 'tenants.deleteDependencies.blockers.catalog',
    audit_logs: 'tenants.deleteDependencies.blockers.auditLogs',
};

export const TENANT_DELETE_NEXT_STEP_KEYS: Record<string, string> = {
    soft_delete_archive: 'tenants.deleteDependencies.nextSteps.softDeleteArchive',
    compliance_soft_delete_only: 'tenants.deleteDependencies.nextSteps.complianceSoftDeleteOnly',
    remove_cash_registers: 'tenants.deleteDependencies.nextSteps.removeCashRegisters',
    review_tenant_users: 'tenants.deleteDependencies.nextSteps.reviewTenantUsers',
    review_fiscal_records: 'tenants.deleteDependencies.nextSteps.reviewFiscalRecords',
    retain_for_rksv_retention: 'tenants.deleteDependencies.nextSteps.retainForRksvRetention',
    eligible_for_dev_permanent_delete: 'tenants.deleteDependencies.nextSteps.eligibleForDevPermanentDelete',
};

export function buildTenantDeletePreparationHref(tenantId: string): string {
    return `/admin/tenants/${tenantId}/delete-preparation`;
}

export function getNonZeroTenantDeleteCounts(
    counts: TenantDeleteDependencyCountsDto | undefined,
): Array<{ row: TenantDeleteCountRow; value: number }> {
    if (!counts) return [];
    return TENANT_DELETE_COUNT_ROWS.flatMap((row) => {
        const value = counts[row.key] ?? 0;
        return value > 0 ? [{ row, value }] : [];
    });
}

export function resolveTenantDeleteFailureMessage(
    t: (key: string, options?: Record<string, string | number>) => string,
    failureCode: string | null | undefined,
    fallbackMessage: string | null | undefined,
): string {
    if (failureCode) {
        const key = TENANT_DELETE_FAILURE_CODE_KEYS[failureCode];
        if (key) return t(key);
    }
    return fallbackMessage?.trim() || t('tenants.deleteDependencies.failureCodes.generic');
}
