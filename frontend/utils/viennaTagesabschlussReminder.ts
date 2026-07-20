/**
 * Hours remaining until the next Europe/Vienna calendar midnight.
 * Used by POS Tagesabschluss reminders as fallback when working hours are unavailable.
 */
export function computeViennaHoursRemainingUntilMidnight(now: Date = new Date()): number {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/Vienna',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(now);

  const read = (type: Intl.DateTimeFormatPartTypes): number =>
    Number(parts.find((p) => p.type === type)?.value ?? 0);

  const secondsIntoDay = read('hour') * 3600 + read('minute') * 60 + read('second');
  const secondsRemaining = Math.max(0, 24 * 3600 - secondsIntoDay);
  return Math.max(0, Math.ceil(secondsRemaining / 3600));
}

export type PosWorkingHoursDay = {
  openTime: string;
  closeTime: string;
  isClosed: boolean;
};

export type PosWorkingHours = {
  reminderHoursBeforeClosing: number;
  monday: PosWorkingHoursDay;
  tuesday: PosWorkingHoursDay;
  wednesday: PosWorkingHoursDay;
  thursday: PosWorkingHoursDay;
  friday: PosWorkingHoursDay;
  saturday: PosWorkingHoursDay;
  sunday: PosWorkingHoursDay;
};

const DAY_KEYS = [
  'sunday',
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
] as const;

type DayKey = (typeof DAY_KEYS)[number];

/**
 * Resolve today's working-hours day from company settings.
 * Supports named days (API) and optional numeric index (0=Sunday).
 */
export function resolveTodayWorkingHoursDay(
  workingHours: PosWorkingHours | Record<number, PosWorkingHoursDay> | null | undefined,
  now: Date = new Date(),
): PosWorkingHoursDay | null {
  if (!workingHours) return null;

  const dayIndex = now.getDay();
  const indexed = (workingHours as Record<number, PosWorkingHoursDay | undefined>)[dayIndex];
  if (indexed && typeof indexed === 'object' && 'isClosed' in indexed) {
    return indexed;
  }

  const named = workingHours as PosWorkingHours;
  const key = DAY_KEYS[dayIndex];
  return named[key] ?? null;
}

/**
 * Parse reminder threshold from settings (API + sketch aliases).
 */
export function resolveReminderHoursBeforeClose(settings: {
  workingHours?: PosWorkingHours | null;
  reminderHoursBeforeClose?: number;
  reminderHoursBeforeClosing?: number;
} | null | undefined): number {
  const fromRoot =
    settings?.reminderHoursBeforeClose ?? settings?.reminderHoursBeforeClosing;
  if (typeof fromRoot === 'number' && Number.isFinite(fromRoot)) {
    return Math.max(0, Math.min(12, fromRoot));
  }
  const fromHours = settings?.workingHours?.reminderHoursBeforeClosing;
  if (typeof fromHours === 'number' && Number.isFinite(fromHours)) {
    return Math.max(0, Math.min(12, fromHours));
  }
  return 1;
}

function parseHhMm(value: string): { hours: number; minutes: number } | null {
  const match = /^([01]?\d|2[0-3]):([0-5]\d)$/.exec(value.trim());
  if (!match) return null;
  return { hours: Number(match[1]), minutes: Number(match[2]) };
}

function getZonedParts(
  now: Date,
  timeZone: string,
): { year: number; month: number; day: number; hour: number; minute: number; second: number; weekday: DayKey } {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    weekday: 'long',
    hourCycle: 'h23',
  }).formatToParts(now);

  const read = (type: Intl.DateTimeFormatPartTypes): string =>
    parts.find((p) => p.type === type)?.value ?? '';

  const weekdayRaw = read('weekday').toLowerCase() as DayKey;
  const weekday = DAY_KEYS.includes(weekdayRaw) ? weekdayRaw : 'monday';

  return {
    year: Number(read('year')),
    month: Number(read('month')),
    day: Number(read('day')),
    hour: Number(read('hour')),
    minute: Number(read('minute')),
    second: Number(read('second')),
    weekday,
  };
}

/**
 * Build a Date for a local civil time in `timeZone` (approximation via offset sampling).
 */
function zonedLocalToUtc(
  year: number,
  month: number,
  day: number,
  hour: number,
  minute: number,
  second: number,
  timeZone: string,
): Date {
  const utcGuess = new Date(Date.UTC(year, month - 1, day, hour, minute, second));
  const parts = getZonedParts(utcGuess, timeZone);
  const asUtcLike = Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute, parts.second);
  const offsetMs = asUtcLike - utcGuess.getTime();
  return new Date(utcGuess.getTime() - offsetMs);
}

function addCalendarDays(
  year: number,
  month: number,
  day: number,
  delta: number,
): { year: number; month: number; day: number } {
  const base = new Date(Date.UTC(year, month - 1, day + delta));
  return {
    year: base.getUTCFullYear(),
    month: base.getUTCMonth() + 1,
    day: base.getUTCDate(),
  };
}

/**
 * Closing instant for the current restaurant day in the tenant time zone.
 * Returns null when the day is marked closed or times are invalid.
 */
export function computeWorkingHoursClosingAt(
  now: Date,
  timeZone: string,
  workingHours: PosWorkingHours,
): Date | null {
  const local = getZonedParts(now, timeZone);
  const day = workingHours[local.weekday];
  if (!day || day.isClosed) return null;

  const open = parseHhMm(day.openTime);
  const close = parseHhMm(day.closeTime);
  if (!open || !close) return null;

  const openMinutes = open.hours * 60 + open.minutes;
  const closeMinutes = close.hours * 60 + close.minutes;
  const nowMinutes = local.hour * 60 + local.minute;

  let closeDate = { year: local.year, month: local.month, day: local.day };

  if (closeMinutes > openMinutes) {
    // Same-calendar-day close (e.g. 09:00–22:00)
    closeDate = { year: local.year, month: local.month, day: local.day };
  } else {
    // Overnight (e.g. 18:00–02:00)
    if (nowMinutes < closeMinutes) {
      closeDate = { year: local.year, month: local.month, day: local.day };
    } else if (nowMinutes >= openMinutes) {
      closeDate = addCalendarDays(local.year, local.month, local.day, 1);
    } else {
      // Gap between close and open — previous overnight already ended
      closeDate = { year: local.year, month: local.month, day: local.day };
    }
  }

  return zonedLocalToUtc(
    closeDate.year,
    closeDate.month,
    closeDate.day,
    close.hours,
    close.minutes,
    0,
    timeZone,
  );
}

export type SmartTagesabschlussReminderState = {
  /** Show the POS banner (never auto-closes). */
  shouldShow: boolean;
  /** Whole hours remaining (ceil), for compact copy. */
  hoursRemaining: number;
  /** Exact seconds until closing (0 when overdue). */
  secondsRemaining: number;
  /** HH:MM:SS countdown string. */
  countdownLabel: string;
  /** Whether working-hours logic was used (vs Vienna midnight fallback). */
  usedWorkingHours: boolean;
  /** Closing time label HH:mm in local zone, when known. */
  closingTimeLabel: string | null;
};

export function formatCountdown(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(s / 3600);
  const minutes = Math.floor((s % 3600) / 60);
  const seconds = s % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

/**
 * Smart Tagesabschluss reminder: show within X hours of restaurant closing (default 1).
 * Falls back to Vienna midnight only when working hours are not configured.
 * Closed / invalid working-hours days suppress the reminder (no midnight fallback).
 */
export function computeSmartTagesabschlussReminder(options: {
  canClose: boolean;
  now?: Date;
  timeZone?: string;
  workingHours?: PosWorkingHours | null;
}): SmartTagesabschlussReminderState {
  const now = options.now ?? new Date();
  const timeZone = options.timeZone?.trim() || 'Europe/Vienna';

  if (!options.canClose) {
    return {
      shouldShow: false,
      hoursRemaining: 0,
      secondsRemaining: 0,
      countdownLabel: formatCountdown(0),
      usedWorkingHours: false,
      closingTimeLabel: null,
    };
  }

  const hoursConfig = options.workingHours;
  if (hoursConfig) {
    const closingAt = computeWorkingHoursClosingAt(now, timeZone, hoursConfig);
    if (closingAt) {
      const reminderHours = Math.max(
        0,
        Math.min(12, hoursConfig.reminderHoursBeforeClosing ?? 1),
      );
      const reminderStartMs = closingAt.getTime() - reminderHours * 3600_000;
      const secondsRemaining = Math.max(0, Math.ceil((closingAt.getTime() - now.getTime()) / 1000));
      const shouldShow = now.getTime() >= reminderStartMs;
      const closeParts = getZonedParts(closingAt, timeZone);
      const closingTimeLabel = `${String(closeParts.hour).padStart(2, '0')}:${String(closeParts.minute).padStart(2, '0')}`;

      return {
        shouldShow,
        hoursRemaining: Math.max(0, Math.ceil(secondsRemaining / 3600)),
        secondsRemaining,
        countdownLabel: formatCountdown(secondsRemaining),
        usedWorkingHours: true,
        closingTimeLabel,
      };
    }

    // Working hours configured but today closed / invalid times → no reminder.
    return {
      shouldShow: false,
      hoursRemaining: 0,
      secondsRemaining: 0,
      countdownLabel: formatCountdown(0),
      usedWorkingHours: false,
      closingTimeLabel: null,
    };
  }

  const hoursRemaining = computeViennaHoursRemainingUntilMidnight(now);
  const secondsRemaining = hoursRemaining * 3600;
  return {
    shouldShow: true,
    hoursRemaining,
    secondsRemaining,
    countdownLabel: formatCountdown(secondsRemaining),
    usedWorkingHours: false,
    closingTimeLabel: '00:00',
  };
}

/**
 * Remind when today's closing is still allowed and there is time left in the Vienna day.
 * @deprecated Prefer {@link computeSmartTagesabschlussReminder}.
 */
export function computePosTagesabschlussClosingRequired(options: {
  canClose: boolean;
}): boolean {
  return options.canClose === true;
}
