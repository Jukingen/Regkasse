import type { ActivitySeverity } from '@/api/manual/activityEvents';

/** Mirrors backend <c>ActivityEventType</c> for settings UI. */
export const ACTIVITY_EVENT_TYPES = [
  'UserCreated',
  'UserUpdated',
  'UserDeleted',
  'CashRegisterOpened',
  'CashRegisterClosed',
  'CashRegisterDecommissioned',
  'LicenseExpiringSoon',
  'LicenseExpired',
  'OfflineQueueGrowing',
  'FinanzOnlineSubmissionFailed',
  'BackupFailed',
  'BackupSucceeded',
  'RestoreDrillFailed',
  'RestoreDrillSucceeded',
  'DailyClosingBackdatedCreated',
  'DailyClosingPendingReminder',
  'OnlineOrderPushedToPos',
  'OnlineOrderPaid',
  'OnlineOrderStatusChanged',
  'OnlineOrderConfirmed',
  'DigitalServiceRequested',
  'RoleCreated',
  'RoleDeleted',
  'RolePermissionsUpdated',
  'UserPermissionOverridesChanged',
  'SystemPermissionChange',
] as const;

export type ActivityEventTypeName = (typeof ACTIVITY_EVENT_TYPES)[number];

export const ACTIVITY_SEVERITIES: ActivitySeverity[] = ['Info', 'Warning', 'Error', 'Critical'];

/** Grouped permission-change toggles for notification settings. */
export const PERMISSION_NOTIFY_GROUPS = {
  roles: ['RoleCreated', 'RoleDeleted', 'RolePermissionsUpdated'] as const satisfies readonly ActivityEventTypeName[],
  userPermissions: ['UserPermissionOverridesChanged'] as const satisfies readonly ActivityEventTypeName[],
  systemChanges: ['SystemPermissionChange'] as const satisfies readonly ActivityEventTypeName[],
} as const;

export type PermissionNotifyGroupKey = keyof typeof PERMISSION_NOTIFY_GROUPS;

/** Defaults when tenant config omits a key (System changes opt-in). */
export const ACTIVITY_EVENT_DEFAULT_ENABLED: Partial<Record<ActivityEventTypeName, boolean>> = {
  SystemPermissionChange: false,
};

export function isPermissionActivityType(type: string): boolean {
  return (
    type === 'RoleCreated' ||
    type === 'RoleDeleted' ||
    type === 'RolePermissionsUpdated' ||
    type === 'UserPermissionOverridesChanged' ||
    type === 'SystemPermissionChange'
  );
}
