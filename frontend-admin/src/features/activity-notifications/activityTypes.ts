import type { ActivitySeverity } from '@/api/manual/activityEvents';

/** Mirrors backend <c>ActivityEventType</c> for settings UI.</c> */
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
] as const;

export type ActivityEventTypeName = (typeof ACTIVITY_EVENT_TYPES)[number];

export const ACTIVITY_SEVERITIES: ActivitySeverity[] = ['Info', 'Warning', 'Error', 'Critical'];
