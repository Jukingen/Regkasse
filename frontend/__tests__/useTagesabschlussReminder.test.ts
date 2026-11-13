import { describe, expect, it } from '@jest/globals';

import {
  resolveReminderHoursBeforeClose,
  resolveTodayWorkingHoursDay,
  type PosWorkingHours,
} from '../utils/viennaTagesabschlussReminder';

const sampleDay = {
  openTime: '09:00',
  closeTime: '22:00',
  isClosed: false,
};

const sampleHours: PosWorkingHours = {
  reminderHoursBeforeClosing: 2,
  sunday: { ...sampleDay, isClosed: true },
  monday: sampleDay,
  tuesday: sampleDay,
  wednesday: sampleDay,
  thursday: sampleDay,
  friday: sampleDay,
  saturday: sampleDay,
};

describe('resolveTodayWorkingHoursDay', () => {
  it('reads named weekday from PosWorkingHours', () => {
    // 2026-07-19 was a Sunday
    const sunday = new Date('2026-07-19T12:00:00.000Z');
    const day = resolveTodayWorkingHoursDay(sampleHours, sunday);
    expect(day?.isClosed).toBe(true);
  });

  it('supports numeric day index (0=Sunday) as in the product sketch', () => {
    const byIndex = {
      0: { openTime: '10:00', closeTime: '18:00', isClosed: false },
    };
    const sunday = new Date('2026-07-19T12:00:00.000Z');
    const day = resolveTodayWorkingHoursDay(byIndex, sunday);
    expect(day?.closeTime).toBe('18:00');
  });
});

describe('resolveReminderHoursBeforeClose', () => {
  it('prefers sketch alias reminderHoursBeforeClose', () => {
    expect(
      resolveReminderHoursBeforeClose({
        reminderHoursBeforeClose: 3,
        workingHours: sampleHours,
      })
    ).toBe(3);
  });

  it('falls back to workingHours.reminderHoursBeforeClosing then 1', () => {
    expect(resolveReminderHoursBeforeClose({ workingHours: sampleHours })).toBe(2);
    expect(resolveReminderHoursBeforeClose(null)).toBe(1);
  });
});
