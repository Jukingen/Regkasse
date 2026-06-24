export { useBillingAccess } from './useBillingAccess';

// Query hooks
export { useBillingStats } from './useBillingStats';
export { useBillingSalesList } from './useBillingSalesList';
export type { BillingSalesListFilters } from './useBillingSalesList';
export { useBillingSale } from './useBillingSale';
export { useBillingSaleByKey } from './useBillingSaleByKey';
export { useExpiringLicenses } from './useExpiringLicenses';
export { useBillingTenantLicense } from './useBillingTenantLicense';
export { useLicenseStatus } from './useLicenseStatus';
export { useBillingAuditLogs } from './useBillingAuditLogs';
export { useTenantReminders } from './useTenantReminders';

// Mutation hooks
export { useBillingPreview } from './useBillingPreview';
export { useBillingCreate } from './useBillingCreate';
export { useCancelLicenseSale } from './useCancelLicenseSale';
export { useActivateLicense } from './useActivateLicense';
export type { ActivateLicenseRequest } from './useActivateLicense';
export { useExtendLicense } from './useExtendLicense';
export type { ExtendLicenseRequest } from './useExtendLicense';
export { useCheckReminders } from './useCheckReminders';
export { useSendReminders } from './useSendReminders';

// Re-export API types used by hooks
export type {
    CreateLicenseSaleRequest,
    LicenseSalePreviewRequest,
    LicenseSaleListQuery,
    BillingAuditLogsFilters,
    BillingStatsParams,
} from '../api/billingApi';
