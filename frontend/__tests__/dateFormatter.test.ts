import { describe, expect, it } from '@jest/globals';

import {
  endOfLocalDay,
  formatDateForHtmlInput,
  formatUserDate,
  formatUserDateTime,
  normalizePickerSelection,
  startOfLocalDay,
} from '../utils/dateFormatter';

describe('dateFormatter', () => {
  it('formats local date as DD.MM.YYYY', () => {
    const local = new Date(2025, 11, 1, 14, 30, 0);
    expect(formatUserDate(local)).toBe('01.12.2025');
  });

  it('formats datetime with seconds', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatUserDateTime(local, { includeSeconds: true })).toBe('01.12.2025 14:30:45');
  });

  it('startOfLocalDay clears time to 00:00:00.000', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45, 123);
    const start = startOfLocalDay(local);
    expect(start.getFullYear()).toBe(2025);
    expect(start.getMonth()).toBe(11);
    expect(start.getDate()).toBe(1);
    expect(start.getHours()).toBe(0);
    expect(start.getMinutes()).toBe(0);
    expect(start.getSeconds()).toBe(0);
    expect(start.getMilliseconds()).toBe(0);
  });

  it('endOfLocalDay sets time to 23:59:59.999', () => {
    const local = new Date(2025, 11, 1, 8, 0, 0, 0);
    const end = endOfLocalDay(local);
    expect(end.getFullYear()).toBe(2025);
    expect(end.getMonth()).toBe(11);
    expect(end.getDate()).toBe(1);
    expect(end.getHours()).toBe(23);
    expect(end.getMinutes()).toBe(59);
    expect(end.getSeconds()).toBe(59);
    expect(end.getMilliseconds()).toBe(999);
  });

  it('normalizePickerSelection maps start/end bounds for date filters', () => {
    const midday = new Date(2026, 6, 21, 12, 0, 0);
    const start = normalizePickerSelection(midday, 'start');
    const end = normalizePickerSelection(midday, 'end');
    const keep = normalizePickerSelection(midday, 'keep');

    expect(formatUserDate(start)).toBe('21.07.2026');
    expect(start.getHours()).toBe(0);
    expect(end.getHours()).toBe(23);
    expect(end.getMinutes()).toBe(59);
    expect(keep.getHours()).toBe(12);
    expect(keep).not.toBe(midday);
  });

  it('quick today range covers full local calendar day', () => {
    const today = new Date(2026, 6, 21, 15, 45, 0);
    const start = startOfLocalDay(today);
    const end = endOfLocalDay(today);
    expect(end.getTime()).toBeGreaterThan(start.getTime());
    expect(formatUserDate(start)).toBe(formatUserDate(end));
  });

  it('formatDateForHtmlInput formats local civil components for web inputs', () => {
    const local = new Date(2026, 6, 21, 9, 5, 0);
    expect(formatDateForHtmlInput(local, 'date')).toBe('2026-07-21');
    expect(formatDateForHtmlInput(local, 'time')).toBe('09:05');
    expect(formatDateForHtmlInput(local, 'datetime')).toBe('2026-07-21T09:05');
  });
});
