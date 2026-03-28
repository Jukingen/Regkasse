import { describe, expect, it } from 'vitest';
import {
  joinFormalReportRemediationHints,
  resolveFormalReportContentFromDualFields,
  resolveFormalReportExportProfileRow,
  resolveFormalReportLegalExportIssueMessage,
} from '../formalReportContentResolver';
import {
  joinFiscalReportRemediationHints,
  resolveFiscalExportProfileRow,
  resolveFiscalReportBackendText,
  resolveLegalExportCompletenessIssueMessage,
} from '@/shared/backendLocale/fiscalReportTextPolicy';

describe('formalReportContentResolver re-exports', () => {
  it('aliases point to the same functions as fiscalReportTextPolicy', () => {
    expect(resolveFormalReportContentFromDualFields).toBe(resolveFiscalReportBackendText);
    expect(joinFormalReportRemediationHints).toBe(joinFiscalReportRemediationHints);
    expect(resolveFormalReportExportProfileRow).toBe(resolveFiscalExportProfileRow);
    expect(resolveFormalReportLegalExportIssueMessage).toBe(resolveLegalExportCompletenessIssueMessage);
  });
});

describe('resolveFormalReportContentFromDualFields (policy matrix)', () => {
  it('UI de → German when present', () => {
    expect(resolveFormalReportContentFromDualFields('DE', 'de', 'EN')).toEqual({
      text: 'DE',
      contentLang: 'de',
    });
  });

  it('UI en → English when present, else German', () => {
    expect(resolveFormalReportContentFromDualFields('DE', 'en', 'EN')).toEqual({ text: 'EN', contentLang: 'en' });
    expect(resolveFormalReportContentFromDualFields('DE', 'en', undefined)).toEqual({ text: 'DE', contentLang: 'de' });
  });

  it('UI tr → never tr; German first, then English', () => {
    expect(resolveFormalReportContentFromDualFields('DE', 'tr', 'EN')).toEqual({ text: 'DE', contentLang: 'de' });
    expect(resolveFormalReportContentFromDualFields('', 'tr', 'EN')).toEqual({ text: 'EN', contentLang: 'en' });
  });
});

describe('resolveFormalReportExportProfileRow', () => {
  const row = {
    profileKey: 'p1',
    labelDe: 'Lde',
    descriptionDe: 'Dde',
    labelEn: 'Len',
    descriptionEn: 'Den',
    includeTraceIds: false,
  };

  it('for tr UI uses German-first formal copy', () => {
    const r = resolveFormalReportExportProfileRow(row, 'tr');
    expect(r?.label).toEqual({ text: 'Lde', contentLang: 'de' });
    expect(r?.description).toEqual({ text: 'Dde', contentLang: 'de' });
  });
});
