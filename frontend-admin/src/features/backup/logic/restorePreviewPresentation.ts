/**
 * Maps backup verification-report payload into a restore preview view-model.
 * Row counts are live/source estimates for tables present in the dump TOC —
 * custom-format dumps do not embed per-table row counts without a restore.
 */

import type { BackupVerificationReport } from "@/features/backup/logic/backupVerificationReportApi";
import { getBackupVerificationRowDiff } from "@/features/backup/logic/backupVerificationReportPresentation";

export type RestorePreviewChangeKind = "inDump" | "missingFromDump" | "mismatch" | "aligned";

export type RestorePreviewChangeRow = {
  key: string;
  table: string;
  count: number;
  changeKind: RestorePreviewChangeKind;
  /** Absolute row delta vs live source when known. */
  diff: number | null;
};

export type RestorePreviewViewModel = {
  backupRunId: string;
  tables: number;
  records: number;
  sizeBytes: number;
  sizeFormatted: string;
  logicalDumpAnalyzed: boolean;
  analysisMessage: string | null;
  changes: RestorePreviewChangeRow[];
};

function changeKindFor(
  presentInDump: boolean,
  mismatched: boolean,
  missingSource: boolean,
): RestorePreviewChangeKind {
  if (!presentInDump) return "missingFromDump";
  if (missingSource) return "inDump";
  if (mismatched) return "mismatch";
  return "aligned";
}

export function mapVerificationReportToRestorePreview(
  report: BackupVerificationReport | null | undefined,
): RestorePreviewViewModel | null {
  if (!report?.backupRunId) return null;

  const tablesInDump = report.tableStatistics.filter((t) => t.presentInLogicalDump);
  const scope = report.logicalDumpAnalyzed ? tablesInDump : report.tableStatistics;

  const changes: RestorePreviewChangeRow[] = scope.map((row) => {
    const diff = getBackupVerificationRowDiff(report, row);
    const tableLabel = row.schemaName
      ? `${row.schemaName}.${row.tableName}`
      : row.tableName;
    return {
      key: tableLabel,
      table: tableLabel,
      count: row.rowCount,
      changeKind: changeKindFor(
        row.presentInLogicalDump,
        diff.mismatched,
        diff.missingSource,
      ),
      diff: diff.missingSource ? null : diff.diff,
    };
  });

  const records = scope.reduce((sum, row) => sum + (row.rowCount || 0), 0);

  return {
    backupRunId: report.backupRunId,
    tables: scope.length,
    records,
    sizeBytes: report.totalSizeBytes ?? 0,
    sizeFormatted: report.totalSizeFormatted || "—",
    logicalDumpAnalyzed: report.logicalDumpAnalyzed,
    analysisMessage: report.logicalDumpAnalysisMessage,
    changes,
  };
}

/** Size in whole MiB for compact Descriptions (sketch-style). */
export function restorePreviewSizeMib(sizeBytes: number): number {
  if (sizeBytes <= 0) return 0;
  return Math.round((sizeBytes / (1024 * 1024)) * 10) / 10;
}
