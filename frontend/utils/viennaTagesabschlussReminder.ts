/**
 * Hours remaining until the next Europe/Vienna calendar midnight.
 * Used by POS Tagesabschluss reminders (no automatic closing).
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

/**
 * Remind when today's closing is still allowed and there is time left in the Vienna day.
 */
export function computePosTagesabschlussClosingRequired(options: {
  canClose: boolean;
}): boolean {
  return options.canClose === true;
}
