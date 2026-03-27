import { describe, expect, it } from 'vitest';
import type { TextLocale } from '@/i18n/config';
import {
  FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES,
  isReportContentLanguage,
  preferredReportContentLanguage,
  type ReportContentLanguage,
} from '../reportContentLanguagePolicy';

describe('FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES', () => {
  it('is fixed de | en', () => {
    expect(FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES).toEqual(['de', 'en']);
  });
});

describe('isReportContentLanguage', () => {
  it.each<[unknown, boolean]>([
    ['de', true],
    ['en', true],
    ['tr', false],
    ['fr', false],
    [null, false],
  ])('(%s) -> %s', (v, expected) => {
    expect(isReportContentLanguage(v)).toBe(expected);
  });
});

describe('preferredReportContentLanguage', () => {
  it.each<[TextLocale, ReportContentLanguage]>([
    ['de', 'de'],
    ['en', 'en'],
    ['tr', 'de'],
  ])('UI %s -> preferred %s', (ui, expected) => {
    expect(preferredReportContentLanguage(ui)).toBe(expected);
  });
});
