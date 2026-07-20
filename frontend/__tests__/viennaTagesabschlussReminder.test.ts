import { describe, expect, it } from '@jest/globals';

import {
  computePosTagesabschlussClosingRequired,
  computeSmartTagesabschlussReminder,
  computeViennaHoursRemainingUntilMidnight,
  computeWorkingHoursClosingAt,
  formatCountdown,
  type PosWorkingHours,
} from '../utils/viennaTagesabschlussReminder';

const openDay = (closeTime: string, openTime = '09:00'): PosWorkingHours['monday'] => ({
  openTime,
  closeTime,
  isClosed: false,
});

function hoursWithClose(closeTime: string, reminderHoursBeforeClosing = 1): PosWorkingHours {
  const day = openDay(closeTime);
  return {
    reminderHoursBeforeClosing,
    monday: day,
    tuesday: day,
    wednesday: day,
    thursday: day,
    friday: day,
    saturday: day,
    sunday: day,
  };
}

describe('computePosTagesabschlussClosingRequired', () => {
  it('is true only when canClose is true', () => {
    expect(computePosTagesabschlussClosingRequired({ canClose: true })).toBe(true);
    expect(computePosTagesabschlussClosingRequired({ canClose: false })).toBe(false);
  });
});

describe('computeViennaHoursRemainingUntilMidnight', () => {
  it('returns a value between 0 and 24 inclusive', () => {
    const hours = computeViennaHoursRemainingUntilMidnight(new Date());
    expect(hours).toBeGreaterThanOrEqual(0);
    expect(hours).toBeLessThanOrEqual(24);
  });

  it('returns 1 when less than one hour remains before Vienna midnight', () => {
    // Fixed UTC instant: 2026-07-16 21:45 UTC = 23:45 Vienna (CEST, UTC+2)
    const nearMidnight = new Date('2026-07-16T21:45:00.000Z');
    expect(computeViennaHoursRemainingUntilMidnight(nearMidnight)).toBe(1);
  });

  it('returns about 12 hours around Vienna noon', () => {
    // 2026-07-16 10:00 UTC = 12:00 Vienna (CEST)
    const noon = new Date('2026-07-16T10:00:00.000Z');
    expect(computeViennaHoursRemainingUntilMidnight(noon)).toBe(12);
  });
});

describe('formatCountdown', () => {
  it('formats HH:MM:SS', () => {
    expect(formatCountdown(3661)).toBe('01:01:01');
    expect(formatCountdown(0)).toBe('00:00:00');
  });
});

describe('computeSmartTagesabschlussReminder', () => {
  it('hides when canClose is false', () => {
    const state = computeSmartTagesabschlussReminder({
      canClose: false,
      now: new Date('2026-07-16T19:00:00.000Z'),
      workingHours: hoursWithClose('22:00'),
    });
    expect(state.shouldShow).toBe(false);
  });

  it('shows within reminder window before working-hours close', () => {
    // Thursday 2026-07-16 21:15 Vienna (CEST) = 19:15 UTC; close 22:00, reminder 1h → window at 21:00
    const now = new Date('2026-07-16T19:15:00.000Z');
    const state = computeSmartTagesabschlussReminder({
      canClose: true,
      now,
      timeZone: 'Europe/Vienna',
      workingHours: hoursWithClose('22:00', 1),
    });
    expect(state.usedWorkingHours).toBe(true);
    expect(state.shouldShow).toBe(true);
    expect(state.closingTimeLabel).toBe('22:00');
    expect(state.countdownLabel).toMatch(/^\d{2}:\d{2}:\d{2}$/);
    expect(state.secondsRemaining).toBeGreaterThan(0);
  });

  it('hides before reminder window', () => {
    // 18:00 Vienna = 16:00 UTC; close 22:00, reminder 1h → starts 21:00
    const now = new Date('2026-07-16T16:00:00.000Z');
    const state = computeSmartTagesabschlussReminder({
      canClose: true,
      now,
      timeZone: 'Europe/Vienna',
      workingHours: hoursWithClose('22:00', 1),
    });
    expect(state.usedWorkingHours).toBe(true);
    expect(state.shouldShow).toBe(false);
  });

  it('hides when the day is closed (no midnight fallback)', () => {
    const closed: PosWorkingHours = {
      ...hoursWithClose('22:00'),
      thursday: { openTime: '09:00', closeTime: '22:00', isClosed: true },
    };
    // 2026-07-16 was a Thursday
    const now = new Date('2026-07-16T18:00:00.000Z');
    const state = computeSmartTagesabschlussReminder({
      canClose: true,
      now,
      timeZone: 'Europe/Vienna',
      workingHours: closed,
    });
    expect(state.usedWorkingHours).toBe(false);
    expect(state.shouldShow).toBe(false);
    expect(state.closingTimeLabel).toBeNull();
  });
});

describe('computeWorkingHoursClosingAt', () => {
  it('resolves same-day closing in Europe/Vienna', () => {
    const now = new Date('2026-07-16T10:00:00.000Z'); // 12:00 Vienna
    const closing = computeWorkingHoursClosingAt(now, 'Europe/Vienna', hoursWithClose('22:00'));
    expect(closing).not.toBeNull();
    // 22:00 Vienna CEST = 20:00 UTC
    expect(closing!.toISOString()).toBe('2026-07-16T20:00:00.000Z');
  });
});
