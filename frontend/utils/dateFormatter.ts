/**
 * Fixed German date display for POS UI (de-DE product copy, DD.MM.YYYY visuals).
 * API payloads must not use these helpers; display only.
 */

export const UI_DATE_PATTERN = 'DD.MM.YYYY' as const;

export type FormatUserDateTimeOptions = {
  includeSeconds?: boolean;
};

function parseInput(input: string | number | Date | null | undefined): Date | null {
  if (input == null || input === '') return null;
  const d = input instanceof Date ? input : new Date(input);
  return Number.isNaN(d.getTime()) ? null : d;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

/** `01.12.2025` — device local timezone from ISO / Date. */
export function formatUserDate(input: string | number | Date | null | undefined): string {
  const d = parseInput(input);
  if (!d) return '';
  return `${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
}

/** `01.12.2025 10:30` or with seconds when requested. */
export function formatUserDateTime(
  input: string | number | Date | null | undefined,
  options?: FormatUserDateTimeOptions
): string {
  const d = parseInput(input);
  if (!d) return '';
  const datePart = formatUserDate(d);
  const h = pad2(d.getHours());
  const m = pad2(d.getMinutes());
  if (options?.includeSeconds) {
    return `${datePart} ${h}:${m}:${pad2(d.getSeconds())}`;
  }
  return `${datePart} ${h}:${m}`;
}

/** Time only `10:30` or `10:30:45`. */
export function formatUserTime(
  input: string | number | Date | null | undefined,
  options?: { includeSeconds?: boolean }
): string {
  const d = parseInput(input);
  if (!d) return '';
  const h = pad2(d.getHours());
  const m = pad2(d.getMinutes());
  if (options?.includeSeconds) {
    return `${h}:${m}:${pad2(d.getSeconds())}`;
  }
  return `${h}:${m}`;
}

/**
 * Local calendar-day start (00:00:00.000) for date-only filters / pickers.
 * Uses the device local timezone (AT registers: typically Europe/Vienna).
 */
export function startOfLocalDay(input: Date): Date {
  const d = new Date(input.getTime());
  d.setHours(0, 0, 0, 0);
  return d;
}

/**
 * Local calendar-day end (23:59:59.999) for inclusive date-range filters.
 */
export function endOfLocalDay(input: Date): Date {
  const d = new Date(input.getTime());
  d.setHours(23, 59, 59, 999);
  return d;
}

/**
 * Normalize a DateTimePicker selection for date-only range bounds.
 * `start` → start of local day; `end` → end of local day; otherwise keep as-is.
 */
export function normalizePickerSelection(
  selected: Date,
  bound: 'start' | 'end' | 'keep' = 'keep'
): Date {
  if (bound === 'start') return startOfLocalDay(selected);
  if (bound === 'end') return endOfLocalDay(selected);
  return new Date(selected.getTime());
}

export type DatePickerMode = 'date' | 'time' | 'datetime';

/**
 * Format a Date for HTML date / time / datetime-local inputs (local civil time).
 * Used by the web DatePicker fallback when native datetimepicker is unavailable.
 */
export function formatDateForHtmlInput(date: Date, mode: DatePickerMode): string {
  const y = date.getFullYear();
  const m = pad2(date.getMonth() + 1);
  const d = pad2(date.getDate());
  const hh = pad2(date.getHours());
  const mm = pad2(date.getMinutes());
  if (mode === 'time') return `${hh}:${mm}`;
  if (mode === 'datetime') return `${y}-${m}-${d}T${hh}:${mm}`;
  return `${y}-${m}-${d}`;
}
