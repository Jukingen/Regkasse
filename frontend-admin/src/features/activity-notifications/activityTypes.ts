import { ActivityEventType } from '@/api/generated/model/activityEventType';
import type { ActivitySeverity } from '@/api/manual/activityEvents';

/** Settings toggles. Values are the generated `ActivityEventType` member names. */
const ACTIVITY_EVENT_TYPES_BASE = [
  ActivityEventType.UserCreated,
  ActivityEventType.UserUpdated,
  ActivityEventType.UserDeleted,
  ActivityEventType.CashRegisterOpened,
  ActivityEventType.CashRegisterClosed,
  ActivityEventType.CashRegisterDecommissioned,
  ActivityEventType.CashRegisterOpenRequested,
  ActivityEventType.CashRegisterOpenRequestApproved,
  ActivityEventType.CashRegisterOpenRequestDenied,
  ActivityEventType.LicenseExpiringSoon,
  ActivityEventType.LicenseExpired,
  ActivityEventType.LimitApproaching,
  ActivityEventType.LimitExceeded,
  ActivityEventType.OfflineQueueGrowing,
  ActivityEventType.OfflineQueueApproachingLimit,
  ActivityEventType.FinanzOnlineSubmissionFailed,
  ActivityEventType.BackupFailed,
  ActivityEventType.BackupSucceeded,
  ActivityEventType.RestoreDrillFailed,
  ActivityEventType.RestoreDrillSucceeded,
  ActivityEventType.DailyClosingBackdatedCreated,
  ActivityEventType.DailyClosingPendingReminder,
  ActivityEventType.DailyClosingAutoCreated,
  ActivityEventType.DailyClosingOpenOrdersWarning,
  ActivityEventType.MonatsbelegMissingReminder,
  ActivityEventType.MonatsbelegAutoCreated,
  ActivityEventType.MonatsbelegCreated,
  ActivityEventType.MonatsbelegAutoCreateFailed,
  ActivityEventType.MonatsbelegAutoCreateMissed,
  ActivityEventType.JahresbelegFonReminder,
  ActivityEventType.MonatsbelegManagerContacted,
  ActivityEventType.DepExportDueSoon,
  ActivityEventType.DepExportOverdue,
  ActivityEventType.DepExportValidationFailed,
  ActivityEventType.OnlineOrderPushedToPos,
  ActivityEventType.OnlineOrderPaid,
  ActivityEventType.OnlineOrderStatusChanged,
  ActivityEventType.OnlineOrderConfirmed,
  ActivityEventType.DigitalServiceRequested,
  ActivityEventType.SupportTicketCreated,
  ActivityEventType.SupportTicketStaffReplied,
  ActivityEventType.SupportTicketTenantReplied,
  ActivityEventType.SupportTicketResolved,
  ActivityEventType.SupportTicketClosed,
  ActivityEventType.RoleCreated,
  ActivityEventType.RoleDeleted,
  ActivityEventType.RolePermissionsUpdated,
  ActivityEventType.UserPermissionOverridesChanged,
  ActivityEventType.SystemPermissionChange,
] as const;

/**
 * Country, QR-Rechnung, e-invoice, and Peppol events.
 * Values are generated <c>ActivityEventType</c> member names.
 */
export const CATALOG_ACTIVITY_GROUPS = {
  countryFiscal: [
    ActivityEventType.TenantCountryChanged,
    ActivityEventType.TenantCountryChangedHistoricalPreserved,
    ActivityEventType.QrRechnungPayloadBuilt,
    ActivityEventType.QrRechnungPdfGenerated,
  ],
  eInvoicing: [
    ActivityEventType.EinvoiceValidated,
    ActivityEventType.EinvoiceSubmitted,
    ActivityEventType.EinvoiceSubmissionFailed,
  ],
  peppol: [ActivityEventType.PeppolParticipantRegistered],
} as const;

export const CATALOG_ACTIVITY_EVENTS = [
  ...CATALOG_ACTIVITY_GROUPS.countryFiscal,
  ...CATALOG_ACTIVITY_GROUPS.eInvoicing,
  ...CATALOG_ACTIVITY_GROUPS.peppol,
] as const;

export const ACTIVITY_EVENT_TYPES = [
  ...ACTIVITY_EVENT_TYPES_BASE,
  ...CATALOG_ACTIVITY_EVENTS,
] as const;

export type ActivityEventTypeName = (typeof ACTIVITY_EVENT_TYPES)[number];

export const ACTIVITY_SEVERITIES: ActivitySeverity[] = ['Info', 'Warning', 'Error', 'Critical'];

/** Grouped permission-change toggles for notification settings. */
export const PERMISSION_NOTIFY_GROUPS = {
  roles: [
    ActivityEventType.RoleCreated,
    ActivityEventType.RoleDeleted,
    ActivityEventType.RolePermissionsUpdated,
  ] as const satisfies readonly ActivityEventTypeName[],
  userPermissions: [
    ActivityEventType.UserPermissionOverridesChanged,
  ] as const satisfies readonly ActivityEventTypeName[],
  systemChanges: [
    ActivityEventType.SystemPermissionChange,
  ] as const satisfies readonly ActivityEventTypeName[],
} as const;

export type PermissionNotifyGroupKey = keyof typeof PERMISSION_NOTIFY_GROUPS;

/** Defaults when tenant config omits a key (System changes opt-in). */
export const ACTIVITY_EVENT_DEFAULT_ENABLED: Partial<Record<ActivityEventTypeName, boolean>> = {
  [ActivityEventType.SystemPermissionChange]: false,
};

export type CatalogActivityGroupKey = keyof typeof CATALOG_ACTIVITY_GROUPS;

const CATALOG_ACTIVITY_EVENT_SET = new Set<string>(CATALOG_ACTIVITY_EVENTS);

export function isCatalogActivityType(type: string): boolean {
  return CATALOG_ACTIVITY_EVENT_SET.has(type);
}

export function isPermissionActivityType(type: string): boolean {
  return (
    type === ActivityEventType.RoleCreated ||
    type === ActivityEventType.RoleDeleted ||
    type === ActivityEventType.RolePermissionsUpdated ||
    type === ActivityEventType.UserPermissionOverridesChanged ||
    type === ActivityEventType.SystemPermissionChange
  );
}
