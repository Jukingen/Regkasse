import { describe, it, expect } from 'vitest';
import dayjs from 'dayjs';
import { normalizeFromDate, normalizeToDate, validateDateRange } from '../dateUtils';

describe('normalizeFromDate', () => {
    it('always produces start-of-day in the result', () => {
        const d = dayjs('2025-06-15T14:30:00');
        const result = normalizeFromDate(d);
        const parsed = dayjs(result);
        // Should be start of the original day (local), converted to ISO/UTC
        expect(parsed.hour()).toBe(d.startOf('day').hour());
        expect(parsed.minute()).toBe(0);
        expect(parsed.second()).toBe(0);
        expect(parsed.millisecond()).toBe(0);
    });

    it('strips time portion regardless of input time', () => {
        const morning = normalizeFromDate(dayjs('2025-06-15T08:00:00'));
        const evening = normalizeFromDate(dayjs('2025-06-15T20:00:00'));
        expect(morning).toBe(evening);
    });

    it('returns a valid ISO string', () => {
        const result = normalizeFromDate(dayjs('2025-01-01'));
        expect(result).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/);
    });
});

describe('normalizeToDate', () => {
    it('returns the same result as normalizeFromDate for the same day', () => {
        const d = dayjs('2025-06-15T10:00:00');
        expect(normalizeFromDate(d)).toBe(normalizeToDate(d));
    });

    it('strips time portion', () => {
        const early = normalizeToDate(dayjs('2025-06-15T01:00:00'));
        const late = normalizeToDate(dayjs('2025-06-15T23:59:59'));
        expect(early).toBe(late);
    });
});

describe('validateDateRange', () => {
    it('returns null for valid range', () => {
        const from = dayjs('2025-06-01');
        const to = dayjs('2025-06-30');
        expect(validateDateRange(from, to)).toBeNull();
    });

    it('returns null when both are null', () => {
        expect(validateDateRange(null, null)).toBeNull();
    });

    it('returns null when only from is set', () => {
        expect(validateDateRange(dayjs('2025-06-01'), null)).toBeNull();
    });

    it('returns null when only to is set', () => {
        expect(validateDateRange(null, dayjs('2025-06-30'))).toBeNull();
    });

    it('returns null when from equals to (single day)', () => {
        const d = dayjs('2025-06-15');
        expect(validateDateRange(d, d)).toBeNull();
    });

    it('returns error when from > to', () => {
        const from = dayjs('2025-06-30');
        const to = dayjs('2025-06-01');
        const result = validateDateRange(from, to);
        expect(result).not.toBeNull();
        expect(result).toContain('before');
    });

    it('returns error for invalid from date', () => {
        const from = dayjs('not-a-date');
        expect(validateDateRange(from, null)).toContain('Invalid');
    });

    it('returns error for invalid to date', () => {
        const to = dayjs('not-a-date');
        expect(validateDateRange(null, to)).toContain('Invalid');
    });

    it('handles DST spring-forward day (CET→CEST)', () => {
        const from = dayjs('2025-03-30');
        const to = dayjs('2025-03-30');
        expect(validateDateRange(from, to)).toBeNull();
        expect(normalizeFromDate(from)).toBe(normalizeToDate(to));
    });

    it('handles DST fall-back day (CEST→CET)', () => {
        const from = dayjs('2025-10-26');
        const to = dayjs('2025-10-26');
        expect(validateDateRange(from, to)).toBeNull();
        expect(normalizeFromDate(from)).toBe(normalizeToDate(to));
    });
});
