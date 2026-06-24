import {
    usePostApiAdminBillingLicenseSalesPreview,
    usePostApiAdminBillingLicenseSalesPreviewPdf,
    usePostApiAdminBillingLicenseSales,
    useGetApiAdminBillingLicenseSales,
    useGetApiAdminBillingLicenseSalesId,
    useGetApiAdminBillingLicenseSalesByKeyLicenseKey,
    useGetApiAdminBillingLicenseSalesIdPdf,
    usePostApiAdminBillingLicenseSalesIdCancel,
    useGetApiAdminBillingStats,
    useGetApiAdminBillingLicenseSalesExpiring,
    useGetApiAdminBillingTenantsTenantIdLicense,
    useGetApiAdminBillingAudit,
    useGetApiAdminBillingTenantsTenantIdReminders,
    usePostApiAdminBillingRemindersCheck,
    usePostApiAdminBillingRemindersSend,
} from '@/api/generated/admin/admin';
import {
    useGetApiLicenseStatus,
    usePostApiLicenseActivate,
    usePostApiLicenseExtend,
} from '@/api/generated/license/license';

import type {
    CreateLicenseSaleRequest,
    LicenseSalePreviewRequest,
    LicenseSalePreviewResponse,
    LicenseSaleResponse,
    LicenseSaleListResponse,
    LicenseSaleStatsResponse,
    CancelLicenseSaleRequest,
    TenantLicenseInfo,
    TenantLicenseStatus,
    ActivationResult,
    ExtendResult,
    ExpiringLicenseInfo,
    BillingAuditLogListResponse,
    LicenseReminderResponse,
    GetApiAdminBillingLicenseSalesParams,
    GetApiAdminBillingAuditParams,
    GetApiAdminBillingStatsParams,
} from '@/api/generated/model';

export type {
    CreateLicenseSaleRequest,
    LicenseSalePreviewRequest,
    LicenseSalePreviewResponse,
    LicenseSaleResponse,
    LicenseSaleListResponse,
    LicenseSaleStatsResponse,
    CancelLicenseSaleRequest,
    TenantLicenseInfo,
    TenantLicenseStatus,
    ActivationResult,
    ExtendResult,
    ExpiringLicenseInfo,
    BillingAuditLogListResponse,
    LicenseReminderResponse,
};

/** Required pagination fields for sales list queries. */
export type LicenseSaleListQuery = GetApiAdminBillingLicenseSalesParams & {
    page: number;
    pageSize: number;
};

export type BillingAuditLogsFilters = GetApiAdminBillingAuditParams;

export type BillingStatsParams = GetApiAdminBillingStatsParams;

export const billingApi = {
    usePreview: usePostApiAdminBillingLicenseSalesPreview,
    usePreviewPdf: usePostApiAdminBillingLicenseSalesPreviewPdf,
    useCreate: usePostApiAdminBillingLicenseSales,
    useList: useGetApiAdminBillingLicenseSales,
    useGet: useGetApiAdminBillingLicenseSalesId,
    useGetByKey: useGetApiAdminBillingLicenseSalesByKeyLicenseKey,
    useDownloadPdf: useGetApiAdminBillingLicenseSalesIdPdf,
    useCancel: usePostApiAdminBillingLicenseSalesIdCancel,
    useStats: useGetApiAdminBillingStats,
    useExpiring: useGetApiAdminBillingLicenseSalesExpiring,
    useTenantLicense: useGetApiAdminBillingTenantsTenantIdLicense,
    useLicenseStatus: useGetApiLicenseStatus,
    useActivate: usePostApiLicenseActivate,
    useExtend: usePostApiLicenseExtend,
    useAudit: useGetApiAdminBillingAudit,
    useReminders: useGetApiAdminBillingTenantsTenantIdReminders,
    useCheckReminders: usePostApiAdminBillingRemindersCheck,
    useSendReminders: usePostApiAdminBillingRemindersSend,
};
