import { describe, expect, it } from 'vitest';

import type { BackupVerificationReport } from '@/features/backup/logic/backupVerificationReportApi';
import {
  backupVerificationAlertType,
  escapeHtml,
  getBackupVerificationRowDiff,
  getSourceRowCount,
  isBackupVerificationRowMismatched,
} from '@/features/backup/logic/backupVerificationReportPresentation';

const report = {
  sourceStatistics: {
    tables: [
      { tableName: 'payments', rowCount: 100, tableExists: true },
      { tableName: 'ghost', rowCount: 1, tableExists: false },
    ],
  },
} as BackupVerificationReport;

describe('backupVerificationReportPresentation', () => {
  it('resolves source row counts', () => {
    expect(getSourceRowCount(report, 'Payments')).toBe(100);
    expect(getSourceRowCount(report, 'ghost')).toBeNull();
    expect(getSourceRowCount(undefined, 'payments')).toBeNull();
  });

  it('computes row diffs and mismatch flags', () => {
    const matched = getBackupVerificationRowDiff(report, {
      tableName: 'payments',
      rowCount: 100,
      isVerified: true,
      presentInLogicalDump: true,
    } as never);
    expect(matched).toMatchObject({ diff: 0, mismatched: false, missingSource: false });

    const mismatched = getBackupVerificationRowDiff(report, {
      tableName: 'payments',
      rowCount: 90,
      isVerified: true,
      presentInLogicalDump: true,
    } as never);
    expect(mismatched.diff).toBe(10);
    expect(mismatched.diffPercent).toBe(10);
    expect(mismatched.mismatched).toBe(true);
    expect(isBackupVerificationRowMismatched(report, {
      tableName: 'missing',
      rowCount: 5,
      isVerified: true,
      presentInLogicalDump: true,
    } as never)).toBe(true);
  });

  it('maps score to alert type and escapes HTML', () => {
    expect(backupVerificationAlertType(95)).toBe('success');
    expect(backupVerificationAlertType(75)).toBe('warning');
    expect(backupVerificationAlertType(10)).toBe('error');
    expect(escapeHtml(`a&b<"c">`)).toBe('a&amp;b&lt;&quot;c&quot;&gt;');
  });
});
