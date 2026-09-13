import { describe, expect, it } from 'vitest';

import {
  restoreVerificationCheckGlyph,
  restoreVerificationStatusLabelKey,
  restoreVerificationVerdictColor,
  restoreVerificationVerdictLabelKey,
} from '@/features/backup/logic/restoreVerificationPresentation';

describe('restoreVerificationPresentation', () => {
  it('maps numeric and named status to the same label key', () => {
    expect(restoreVerificationStatusLabelKey(2)).toBe(
      'backupDr.restoreVerificationPage.status.succeeded'
    );
    expect(restoreVerificationStatusLabelKey('Succeeded')).toBe(
      'backupDr.restoreVerificationPage.status.succeeded'
    );
  });

  it('uses green PASS and red FAIL', () => {
    expect(restoreVerificationVerdictColor('passed')).toBe('success');
    expect(restoreVerificationVerdictColor('failed')).toBe('error');
    expect(restoreVerificationVerdictLabelKey('passed')).toBe(
      'backupDr.restoreVerificationPage.verdict.passed'
    );
    expect(restoreVerificationCheckGlyph('passed')).toBe('✅');
    expect(restoreVerificationCheckGlyph('failed')).toBe('❌');
  });
});
