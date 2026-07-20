/**
 * Restaurant schedule status from tenant working hours (Europe/Vienna by default).
 * Pure helpers — used by {@link useWorkingHours} for **display / reminders only**.
 *
 * CRITICAL: Never use these values to block POS orders, payments, or register access.
 * Online-order cutoffs belong on Web/App (sites) + public API — not on the POS client.
 */

import {
  computeWorkingHoursClosingAt,
  type PosWorkingHours,
  type PosWorkingHoursDay,
} from './viennaTagesabschlussReminder';

export type PosWorkingHoursSpecialDay = {
  date: string;
  isClosed: boolean;
  openTime?: string | null;
  closeTime?: string | null;
};

export type PosWorkingHoursExtended = PosWorkingHours & {
  stopOnlineOrdersMinutesBeforeClose?: number;
  autoClosePOSAtClosing?: boolean;
  closedDayMessage?: string;
  specialDays?: PosWorkingHoursSpecialDay[];
};

export type PosWorkingHoursStatus = {
  /**
   * Always `true` on POS. Cashiers may always take orders and payments
   * regardless of restaurant working hours.
   */
  posOperationsAllowed: true;
  /**
   * Restaurant schedule window (display only). Does **not** gate POS operations.
   * Prefer this name over the legacy `isOpen` alias.
   */
  restaurantIsOpen: boolean;
  /**
   * @deprecated Alias of {@link restaurantIsOpen} — schedule display only, never a POS gate.
   */
  isOpen: boolean;
  /** True when within the reminder window before restaurant closing (display / Tagesabschluss hint). */
  isClosingSoon: boolean;
  /** Same as {@link isClosingSoon} — explicit reminder flag for UI. */
  showReminder: boolean;
  /** Minutes until restaurant closing (0 when outside open window / unknown). */
  timeUntilClose: number;
  /** Minutes until next restaurant opening (0 when currently in open window). */
  timeUntilOpen: number;
  isSpecialDay: boolean;
  /** German POS status message (informational). */
  message: string;
  /**
   * FA preference: show a strong closing prompt. Never triggers automatic Tagesabschluss.
   */
  preferClosingPrompt: boolean;
  /** Informational — online intake cutoff minutes (enforced on Web/App, not POS). */
  stopOnlineOrdersMinutesBeforeClose: number;
  /** Today's effective local open time (`HH:mm`), display only. */
  openTime: string | null;
  /** Today's effective local close time (`HH:mm`), display only. */
  closeTime: string | null;
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

const DEFAULT_CLOSED_MESSAGE = 'Heute geschlossen';
const DEFAULT_OPEN_MESSAGE = 'Geöffnet';
const DEFAULT_CLOSING_SOON_MESSAGE = 'Schließung bald';

function parseHhMm(value: string): { hours: number; minutes: number } | null {
  const match = /^([01]?\d|2[0-3]):([0-5]\d)$/.exec(value.trim());
  if (!match) return null;
  return { hours: Number(match[1]), minutes: Number(match[2]) };
}

function getZonedParts(
  now: Date,
  timeZone: string,
): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
  weekday: DayKey;
  dateKey: string;
} {
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
  const year = Number(read('year'));
  const month = Number(read('month'));
  const day = Number(read('day'));

  return {
    year,
    month,
    day,
    hour: Number(read('hour')),
    minute: Number(read('minute')),
    second: Number(read('second')),
    weekday,
    dateKey: `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`,
  };
}

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
  const asUtcLike = Date.UTC(
    parts.year,
    parts.month - 1,
    parts.day,
    parts.hour,
    parts.minute,
    parts.second,
  );
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

function weekdayForDate(year: number, month: number, day: number): DayKey {
  // Use UTC noon to avoid DST edge ambiguity for weekday of a civil date.
  const dow = new Date(Date.UTC(year, month - 1, day, 12, 0, 0)).getUTCDay();
  return DAY_KEYS[dow];
}

function findSpecialDay(
  specialDays: PosWorkingHoursSpecialDay[] | undefined,
  dateKey: string,
): PosWorkingHoursSpecialDay | null {
  if (!specialDays?.length) return null;
  return specialDays.find((s) => s.date === dateKey) ?? null;
}

/**
 * Effective hours for a local civil date (special day overrides weekday).
 */
export function resolveEffectiveWorkingHoursDay(
  workingHours: PosWorkingHoursExtended,
  year: number,
  month: number,
  day: number,
): { day: PosWorkingHoursDay; isSpecialDay: boolean } {
  const dateKey = `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  const special = findSpecialDay(workingHours.specialDays, dateKey);
  if (special) {
    if (special.isClosed) {
      return {
        day: { openTime: '00:00', closeTime: '00:00', isClosed: true },
        isSpecialDay: true,
      };
    }
    return {
      day: {
        openTime: special.openTime?.trim() || '09:00',
        closeTime: special.closeTime?.trim() || '22:00',
        isClosed: false,
      },
      isSpecialDay: true,
    };
  }

  const key = weekdayForDate(year, month, day);
  const weekday = workingHours[key] ?? {
    openTime: '09:00',
    closeTime: '22:00',
    isClosed: false,
  };
  return { day: weekday, isSpecialDay: false };
}

function isWithinOpenWindow(
  nowMinutes: number,
  openMinutes: number,
  closeMinutes: number,
): boolean {
  if (closeMinutes > openMinutes) {
    return nowMinutes >= openMinutes && nowMinutes < closeMinutes;
  }
  // Overnight (e.g. 18:00–02:00)
  return nowMinutes >= openMinutes || nowMinutes < closeMinutes;
}

function minutesUntil(from: Date, to: Date): number {
  return Math.max(0, (to.getTime() - from.getTime()) / (60_000));
}

function findNextOpeningAt(
  workingHours: PosWorkingHoursExtended,
  timeZone: string,
  fromLocal: { year: number; month: number; day: number; hour: number; minute: number },
  now: Date,
): Date | null {
  for (let offset = 0; offset < 8; offset += 1) {
    const date = addCalendarDays(fromLocal.year, fromLocal.month, fromLocal.day, offset);
    const { day } = resolveEffectiveWorkingHoursDay(
      workingHours,
      date.year,
      date.month,
      date.day,
    );
    if (day.isClosed) continue;

    const open = parseHhMm(day.openTime);
    if (!open) continue;

    if (offset === 0) {
      const nowMinutes = fromLocal.hour * 60 + fromLocal.minute;
      const openMinutes = open.hours * 60 + open.minutes;
      const close = parseHhMm(day.closeTime);
      if (!close) continue;
      const closeMinutes = close.hours * 60 + close.minutes;
      if (isWithinOpenWindow(nowMinutes, openMinutes, closeMinutes)) {
        return null; // already open
      }
      if (closeMinutes > openMinutes) {
        if (nowMinutes < openMinutes) {
          return zonedLocalToUtc(
            date.year,
            date.month,
            date.day,
            open.hours,
            open.minutes,
            0,
            timeZone,
          );
        }
        continue; // after close today
      }
      // Overnight: gap between close and open
      if (nowMinutes >= closeMinutes && nowMinutes < openMinutes) {
        return zonedLocalToUtc(
          date.year,
          date.month,
          date.day,
          open.hours,
          open.minutes,
          0,
          timeZone,
        );
      }
      continue;
    }

    return zonedLocalToUtc(
      date.year,
      date.month,
      date.day,
      open.hours,
      open.minutes,
      0,
      timeZone,
    );
  }
  return null;
}

/**
 * Compute restaurant schedule status for the current instant.
 * Always sets {@link PosWorkingHoursStatus.posOperationsAllowed} to `true`.
 */
export function computePosWorkingHoursStatus(options: {
  now?: Date;
  timeZone?: string;
  workingHours?: PosWorkingHoursExtended | null;
}): PosWorkingHoursStatus {
  const now = options.now ?? new Date();
  const timeZone = options.timeZone?.trim() || 'Europe/Vienna';
  const hours = options.workingHours;
  const closedMessage = hours?.closedDayMessage?.trim() || DEFAULT_CLOSED_MESSAGE;
  const stopOnline =
    typeof hours?.stopOnlineOrdersMinutesBeforeClose === 'number'
      ? Math.max(0, Math.min(180, hours.stopOnlineOrdersMinutesBeforeClose))
      : 30;
  const preferClosingPrompt = hours?.autoClosePOSAtClosing === true;

  const build = (
    partial: Omit<
      PosWorkingHoursStatus,
      | 'posOperationsAllowed'
      | 'isOpen'
      | 'showReminder'
      | 'preferClosingPrompt'
      | 'stopOnlineOrdersMinutesBeforeClose'
    > & { restaurantIsOpen: boolean; isClosingSoon: boolean },
  ): PosWorkingHoursStatus => ({
    posOperationsAllowed: true,
    restaurantIsOpen: partial.restaurantIsOpen,
    isOpen: partial.restaurantIsOpen,
    isClosingSoon: partial.isClosingSoon,
    showReminder: partial.isClosingSoon,
    timeUntilClose: partial.timeUntilClose,
    timeUntilOpen: partial.timeUntilOpen,
    isSpecialDay: partial.isSpecialDay,
    message: partial.message,
    preferClosingPrompt,
    stopOnlineOrdersMinutesBeforeClose: stopOnline,
    openTime: partial.openTime,
    closeTime: partial.closeTime,
  });

  const emptyClosed = build({
    restaurantIsOpen: false,
    isClosingSoon: false,
    timeUntilClose: 0,
    timeUntilOpen: 0,
    isSpecialDay: false,
    message: closedMessage,
    openTime: null,
    closeTime: null,
  });

  if (!hours) {
    return emptyClosed;
  }

  const local = getZonedParts(now, timeZone);
  const { day: todayHours, isSpecialDay } = resolveEffectiveWorkingHoursDay(
    hours,
    local.year,
    local.month,
    local.day,
  );

  if (todayHours.isClosed) {
    const nextOpen = findNextOpeningAt(hours, timeZone, local, now);
    return build({
      restaurantIsOpen: false,
      isClosingSoon: false,
      timeUntilClose: 0,
      timeUntilOpen: nextOpen ? minutesUntil(now, nextOpen) : 0,
      isSpecialDay,
      message: closedMessage,
      openTime: null,
      closeTime: null,
    });
  }

  const open = parseHhMm(todayHours.openTime);
  const close = parseHhMm(todayHours.closeTime);
  if (!open || !close) {
    return build({
      restaurantIsOpen: false,
      isClosingSoon: false,
      timeUntilClose: 0,
      timeUntilOpen: 0,
      isSpecialDay,
      message: closedMessage,
      openTime: null,
      closeTime: null,
    });
  }

  const nowMinutes = local.hour * 60 + local.minute;
  const openMinutes = open.hours * 60 + open.minutes;
  const closeMinutes = close.hours * 60 + close.minutes;
  const restaurantIsOpen = isWithinOpenWindow(nowMinutes, openMinutes, closeMinutes);

  // Build a PosWorkingHours snapshot with today's effective day on the weekday slot
  // so computeWorkingHoursClosingAt (weekday lookup) still works for overnight.
  const closingHours: PosWorkingHours = {
    ...hours,
    [local.weekday]: todayHours,
  };

  if (restaurantIsOpen) {
    const closingAt = computeWorkingHoursClosingAt(now, timeZone, closingHours);
    const timeUntilClose = closingAt ? minutesUntil(now, closingAt) : 0;
    const reminderHours = Math.max(0, Math.min(12, hours.reminderHoursBeforeClosing ?? 1));
    const closingSoonMinutes = Math.max(1, reminderHours * 60);
    const isClosingSoon = timeUntilClose > 0 && timeUntilClose < closingSoonMinutes;

    return build({
      restaurantIsOpen: true,
      isClosingSoon,
      timeUntilClose,
      timeUntilOpen: 0,
      isSpecialDay,
      message: isClosingSoon ? DEFAULT_CLOSING_SOON_MESSAGE : DEFAULT_OPEN_MESSAGE,
      openTime: todayHours.openTime,
      closeTime: todayHours.closeTime,
    });
  }

  const nextOpen = findNextOpeningAt(hours, timeZone, local, now);
  return build({
    restaurantIsOpen: false,
    isClosingSoon: false,
    timeUntilClose: 0,
    timeUntilOpen: nextOpen ? minutesUntil(now, nextOpen) : 0,
    isSpecialDay,
    message: closedMessage,
    openTime: todayHours.openTime,
    closeTime: todayHours.closeTime,
  });
}
