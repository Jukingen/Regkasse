import type {
  BackupTableStatistics,
  BackupVerificationReport,
} from '@/features/backup/logic/backupVerificationReportApi';

export type BackupVerificationRowDiff = {
  sourceRowCount: number | null;
  diff: number;
  diffPercent: number | null;
  missingSource: boolean;
  mismatched: boolean;
};

export function getSourceRowCount(
  report: BackupVerificationReport | undefined,
  tableName: string
): number | null {
  const source = report?.sourceStatistics?.tables.find(
    (t) => t.tableName.toLowerCase() === tableName.toLowerCase()
  );
  if (!source?.tableExists) return null;
  return source.rowCount;
}

export function getBackupVerificationRowDiff(
  report: BackupVerificationReport | undefined,
  record: BackupTableStatistics
): BackupVerificationRowDiff {
  const sourceRowCount = getSourceRowCount(report, record.tableName);
  const missingSource = sourceRowCount === null;
  const backupRows = record.rowCount;
  const diff = missingSource ? backupRows : Math.abs(backupRows - sourceRowCount);
  const diffPercent =
    !missingSource && sourceRowCount > 0
      ? Number(((diff / sourceRowCount) * 100).toFixed(1))
      : null;
  const mismatched =
    !record.isVerified ||
    !record.presentInLogicalDump ||
    missingSource ||
    (!missingSource && diff > 0);

  return {
    sourceRowCount,
    diff,
    diffPercent,
    missingSource,
    mismatched,
  };
}

export function isBackupVerificationRowMismatched(
  report: BackupVerificationReport | undefined,
  record: BackupTableStatistics
): boolean {
  return getBackupVerificationRowDiff(report, record).mismatched;
}

export function backupVerificationAlertType(score: number): 'success' | 'warning' | 'error' {
  if (score >= 90) return 'success';
  if (score >= 70) return 'warning';
  return 'error';
}

export function escapeHtml(text: string): string {
  return text
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}
