import {
  useGetApiAdminBillingAudit,
  useGetApiAdminBillingLicenseSales,
  useGetApiAdminBillingLicenseSalesByKeyLicenseKey,
  useGetApiAdminBillingLicenseSalesExpiring,
  useGetApiAdminBillingLicenseSalesId,
  useGetApiAdminBillingLicenseSalesIdPdf,
  useGetApiAdminBillingStats,
  useGetApiAdminBillingTenantsTenantIdLicense,
  useGetApiAdminBillingTenantsTenantIdReminders,
  usePostApiAdminBillingLicenseSales,
  usePostApiAdminBillingLicenseSalesIdCancel,
  usePostApiAdminBillingLicenseSalesPreview,
  usePostApiAdminBillingLicenseSalesPreviewPdf,
  usePostApiAdminBillingRemindersCheck,
  usePostApiAdminBillingRemindersSend,
} from '@/api/generated/admin/admin';
import {
  useGetApiLicenseStatus,
  usePostApiLicenseActivate,
  usePostApiLicenseExtend,
} from '@/api/generated/license/license';
import type {
  ActivationResult,
  BillingAuditLogListResponse,
  CancelLicenseSaleRequest,
  CreateLicenseSaleRequest,
  ExpiringLicenseInfo,
  ExtendResult,
  GetApiAdminBillingAuditParams,
  GetApiAdminBillingLicenseSalesParams,
  GetApiAdminBillingStatsParams,
  LicenseReminderResponse,
  LicenseSaleListResponse,
  LicenseSalePreviewRequest,
  LicenseSalePreviewResponse,
  LicenseSaleResponse,
  LicenseSaleStatsResponse,
  TenantLicenseInfo,
  TenantLicenseStatus,
} from '@/api/generated/model';

export type {
  ActivationResult,
  BillingAuditLogListResponse,
  CancelLicenseSaleRequest,
  CreateLicenseSaleRequest,
  ExpiringLicenseInfo,
  ExtendResult,
  LicenseReminderResponse,
  LicenseSaleListResponse,
  LicenseSalePreviewRequest,
  LicenseSalePreviewResponse,
  LicenseSaleResponse,
  LicenseSaleStatsResponse,
  TenantLicenseInfo,
  TenantLicenseStatus,
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
