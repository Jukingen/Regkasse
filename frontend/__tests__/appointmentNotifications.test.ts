import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { Platform } from 'react-native';

import {
  appointmentReminderFireAt,
  appointmentReminderNotificationId,
  scheduleAppointmentReminder,
} from '../services/appointmentNotifications';

const mockSchedule = jest.fn<(...args: unknown[]) => Promise<string>>();
const mockGetPermissions = jest.fn<(...args: unknown[]) => Promise<{ granted: boolean }>>();
const mockRequestPermissions = jest.fn<(...args: unknown[]) => Promise<{ granted: boolean }>>();
const mockSetChannel = jest.fn<(...args: unknown[]) => Promise<void>>();
const mockSetHandler = jest.fn();

jest.mock('expo-notifications', () => ({
  AndroidImportance: { DEFAULT: 3 },
  IosAuthorizationStatus: { PROVISIONAL: 2 },
  SchedulableTriggerInputTypes: { DATE: 'date' },
  setNotificationHandler: (...args: unknown[]) => mockSetHandler(...args),
  setNotificationChannelAsync: (...args: unknown[]) => mockSetChannel(...args),
  getPermissionsAsync: (...args: unknown[]) => mockGetPermissions(...args),
  requestPermissionsAsync: (...args: unknown[]) => mockRequestPermissions(...args),
  scheduleNotificationAsync: (...args: unknown[]) => mockSchedule(...args),
}));

describe('appointmentNotifications', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetPermissions.mockResolvedValue({ granted: true });
    mockSchedule.mockResolvedValue('scheduled');
    Object.defineProperty(Platform, 'OS', { configurable: true, value: 'ios' });
  });

  it('fires 15 minutes before start', () => {
    const fireAt = appointmentReminderFireAt(
      '2026-10-10T10:00:00.000Z',
      Date.parse('2026-10-10T09:00:00.000Z')
    );
    expect(fireAt?.toISOString()).toBe('2026-10-10T09:45:00.000Z');
    expect(appointmentReminderNotificationId('apt-1')).toBe('appointment-reminder-apt-1');
  });

  it('schedules a device-local DATE notification', async () => {
    const start = new Date(Date.now() + 30 * 60_000).toISOString();
    const identifier = await scheduleAppointmentReminder('apt-1', start, {
      title: 'Termin in 15 Minuten',
      body: 'Termin mit Anna beginnt in 15 Minuten.',
    });

    expect(identifier).toBe('appointment-reminder-apt-1');
    expect(mockSchedule).toHaveBeenCalledWith(
      expect.objectContaining({
        identifier: 'appointment-reminder-apt-1',
        content: expect.objectContaining({
          title: 'Termin in 15 Minuten',
          data: { screen: 'appointments', appointmentId: 'apt-1' },
        }),
        trigger: expect.objectContaining({
          type: 'date',
        }),
      })
    );
  });

  it('does not schedule when the reminder time is in the past or the platform is web', async () => {
    const past = await scheduleAppointmentReminder(
      'apt-past',
      new Date(Date.now() - 60_000).toISOString(),
      { title: 't', body: 'b' }
    );
    expect(past).toBeNull();
    expect(mockSchedule).not.toHaveBeenCalled();

    Object.defineProperty(Platform, 'OS', { configurable: true, value: 'web' });
    const web = await scheduleAppointmentReminder(
      'apt-web',
      new Date(Date.now() + 30 * 60_000).toISOString(),
      { title: 't', body: 'b' }
    );
    expect(web).toBeNull();
    expect(mockSchedule).not.toHaveBeenCalled();
  });
});
