/** Milestone offsets (days before expiry) for local license reminder notifications. */
export const LICENSE_REMINDER_DAYS = [30, 14, 7, 1] as const;

export type LicenseReminderDay = (typeof LICENSE_REMINDER_DAYS)[number];

export type LicenseReminderPlanItem = {
  days: LicenseReminderDay;
  fireAt: Date;
};

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Builds future reminder fire times for an expiry date.
 * Milestones already in the past are omitted (caller may show in-app banners instead).
 */
export function planLicenseReminderFireTimes(
  expiryDate: Date,
  nowMs = Date.now(),
  reminderDays: readonly LicenseReminderDay[] = LICENSE_REMINDER_DAYS
): LicenseReminderPlanItem[] {
  const expiryMs = expiryDate.getTime();
  if (!Number.isFinite(expiryMs)) {
    return [];
  }

  const plans: LicenseReminderPlanItem[] = [];
  for (const days of reminderDays) {
    const fireAtMs = expiryMs - days * DAY_MS;
    if (fireAtMs <= nowMs) {
      continue;
    }
    plans.push({ days, fireAt: new Date(fireAtMs) });
  }
  return plans;
}

export function licenseReminderNotificationId(days: number): string {
  return `license-expiry-${days}d`;
}
