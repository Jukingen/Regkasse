import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

import {
  LICENSE_REMINDER_DAYS,
  licenseReminderNotificationId,
  planLicenseReminderFireTimes,
  type LicenseReminderDay,
} from '../utils/licenseReminderSchedule';

const ANDROID_CHANNEL_ID = 'license-reminders';

export type LicenseReminderCopy = {
  titleForDays: (days: LicenseReminderDay) => string;
  body: string;
};

const DEFAULT_COPY: LicenseReminderCopy = {
  titleForDays: (days) => {
    if (days === 1) return 'Lizenz läuft morgen ab';
    return `Lizenz läuft in ${days} Tagen ab`;
  },
  body: 'Bitte verlängern Sie Ihre Lizenz rechtzeitig.',
};

let handlerConfigured = false;

function ensureNotificationHandler(): void {
  if (handlerConfigured || Platform.OS === 'web') return;
  handlerConfigured = true;
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldPlaySound: true,
      shouldSetBadge: false,
      shouldShowBanner: true,
      shouldShowList: true,
    }),
  });
}

function supportsLocalNotifications(): boolean {
  return Platform.OS === 'ios' || Platform.OS === 'android';
}

async function ensureAndroidChannel(): Promise<void> {
  if (Platform.OS !== 'android') return;
  await Notifications.setNotificationChannelAsync(ANDROID_CHANNEL_ID, {
    name: 'Lizenz-Erinnerungen',
    importance: Notifications.AndroidImportance.DEFAULT,
    vibrationPattern: [0, 250, 250, 250],
  });
}

/** Requests local notification permission when not already granted. */
export async function ensureLicenseNotificationPermissions(): Promise<boolean> {
  if (!supportsLocalNotifications()) return false;

  ensureNotificationHandler();
  await ensureAndroidChannel();

  const existing = await Notifications.getPermissionsAsync();
  if (existing.granted || existing.ios?.status === Notifications.IosAuthorizationStatus.PROVISIONAL) {
    return true;
  }

  const requested = await Notifications.requestPermissionsAsync({
    ios: {
      allowAlert: true,
      allowBadge: true,
      allowSound: true,
    },
  });
  return (
    requested.granted ||
    requested.ios?.status === Notifications.IosAuthorizationStatus.PROVISIONAL
  );
}

/** Cancels previously scheduled license-expiry reminder notifications. */
export async function cancelLicenseReminders(): Promise<void> {
  if (!supportsLocalNotifications()) return;

  await Promise.all(
    LICENSE_REMINDER_DAYS.map((days) =>
      Notifications.cancelScheduledNotificationAsync(licenseReminderNotificationId(days)).catch(
        () => undefined
      )
    )
  );
}

/**
 * Schedules local notifications at 30 / 14 / 7 / 1 days before license expiry.
 * Replaces any previously scheduled license reminders for this device.
 */
export async function scheduleLicenseReminders(
  expiryDate: Date,
  copy: LicenseReminderCopy = DEFAULT_COPY
): Promise<string[]> {
  if (!supportsLocalNotifications()) {
    return [];
  }

  if (!(expiryDate instanceof Date) || !Number.isFinite(expiryDate.getTime())) {
    return [];
  }

  const permitted = await ensureLicenseNotificationPermissions();
  if (!permitted) {
    return [];
  }

  await cancelLicenseReminders();

  const plans = planLicenseReminderFireTimes(expiryDate);
  const scheduledIds: string[] = [];

  for (const plan of plans) {
    const identifier = licenseReminderNotificationId(plan.days);
    await Notifications.scheduleNotificationAsync({
      identifier,
      content: {
        title: copy.titleForDays(plan.days),
        body: copy.body,
        data: { screen: 'license', daysBeforeExpiry: plan.days },
        sound: true,
      },
      trigger: {
        type: Notifications.SchedulableTriggerInputTypes.DATE,
        date: plan.fireAt,
        channelId: Platform.OS === 'android' ? ANDROID_CHANNEL_ID : undefined,
      },
    });
    scheduledIds.push(identifier);
  }

  return scheduledIds;
}
