import { describe, expect, it } from 'vitest';
import {
  joinFiscalReportRemediationHints,
  resolveFiscalReportBackendText,
  resolveLegalExportCompletenessIssueMessage,
} from '../fiscalReportTextPolicy';

describe('resolveFiscalReportBackendText', () => {
  it('prefers German for de UI', () => {
    expect(resolveFiscalReportBackendText('Hallo', 'de', 'Hello')).toEqual({
      text: 'Hallo',
      contentLang: 'de',
    });
  });

  it('prefers English for en UI when present', () => {
    expect(resolveFiscalReportBackendText('Hallo', 'en', 'Hello')).toEqual({
      text: 'Hello',
      contentLang: 'en',
    });
  });

  it('falls back to German for en UI when English missing', () => {
    expect(resolveFiscalReportBackendText('Hallo', 'en', undefined)).toEqual({
      text: 'Hallo',
      contentLang: 'de',
    });
  });

  it('never yields Turkish; for tr UI prefers German over English', () => {
    expect(resolveFiscalReportBackendText('Hallo', 'tr', 'Hello')).toEqual({
      text: 'Hallo',
      contentLang: 'de',
    });
  });

  it('for tr UI uses English only when German empty', () => {
    expect(resolveFiscalReportBackendText('', 'tr', 'Hello')).toEqual({
      text: 'Hello',
      contentLang: 'en',
    });
  });

  it('returns undefined when both empty', () => {
    expect(resolveFiscalReportBackendText('  ', 'de', null)).toBeUndefined();
  });
});

describe('joinFiscalReportRemediationHints', () => {
  it('joins resolved lines', () => {
    const r = joinFiscalReportRemediationHints(['a', 'b'], 'de', ' | ');
    expect(r).toEqual({ text: 'a | b', contentLang: 'de' });
  });
});

describe('resolveLegalExportCompletenessIssueMessage', () => {
  it('matches resolveFiscalReportBackendText for issue shape', () => {
    const issue = { messageDe: 'Block', messageEn: 'Block EN' };
    expect(resolveLegalExportCompletenessIssueMessage(issue, 'tr')).toBe('Block');
    expect(resolveLegalExportCompletenessIssueMessage(issue, 'en')).toBe('Block EN');
  });

  it('returns empty string when no text', () => {
    expect(resolveLegalExportCompletenessIssueMessage({ messageDe: '  ' }, 'de')).toBe('');
  });
});
