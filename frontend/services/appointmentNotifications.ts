import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

const ANDROID_CHANNEL_ID = 'appointment-reminders';
const REMINDER_LEAD_MS = 15 * 60 * 1000;

let handlerConfigured = false;

export function appointmentReminderNotificationId(appointmentId: string): string {
  return `appointment-reminder-${appointmentId}`;
}

export function appointmentReminderFireAt(
  startUtc: string,
  nowMs: number = Date.now()
): Date | null {
  const start = Date.parse(startUtc);
  if (!Number.isFinite(start)) return null;
  const fireAtMs = start - REMINDER_LEAD_MS;
  if (fireAtMs <= nowMs) return null;
  return new Date(fireAtMs);
}

function supportsLocalNotifications(): boolean {
  return Platform.OS === 'ios' || Platform.OS === 'android';
}

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

async function ensureAndroidChannel(): Promise<void> {
  if (Platform.OS !== 'android') return;
  await Notifications.setNotificationChannelAsync(ANDROID_CHANNEL_ID, {
    name: 'Termin-Erinnerungen',
    importance: Notifications.AndroidImportance.DEFAULT,
    vibrationPattern: [0, 250, 250, 250],
  });
}

async function ensurePermissions(): Promise<boolean> {
  if (!supportsLocalNotifications()) return false;
  ensureNotificationHandler();
  await ensureAndroidChannel();
  const existing = await Notifications.getPermissionsAsync();
  if (
    existing.granted ||
    existing.ios?.status === Notifications.IosAuthorizationStatus.PROVISIONAL
  ) {
    return true;
  }
  const requested = await Notifications.requestPermissionsAsync({
    ios: { allowAlert: true, allowBadge: true, allowSound: true },
  });
  return (
    requested.granted ||
    requested.ios?.status === Notifications.IosAuthorizationStatus.PROVISIONAL
  );
}

export type AppointmentReminderCopy = {
  title: string;
  body: string;
};

/** Device-local reminder 15 minutes before start. No server push. */
export async function scheduleAppointmentReminder(
  appointmentId: string,
  startUtc: string,
  copy: AppointmentReminderCopy
): Promise<string | null> {
  if (!supportsLocalNotifications()) return null;
  const fireAt = appointmentReminderFireAt(startUtc);
  if (!fireAt) return null;
  const permitted = await ensurePermissions();
  if (!permitted) return null;

  const identifier = appointmentReminderNotificationId(appointmentId);
  await Notifications.scheduleNotificationAsync({
    identifier,
    content: {
      title: copy.title,
      body: copy.body,
      data: { screen: 'appointments', appointmentId },
      sound: true,
    },
    trigger: {
      type: Notifications.SchedulableTriggerInputTypes.DATE,
      date: fireAt,
      channelId: Platform.OS === 'android' ? ANDROID_CHANNEL_ID : undefined,
    },
  });
  return identifier;
}
