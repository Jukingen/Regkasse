/**
 * Compare two backup verification reports by logical-dump TOC presence.
 * Row counts in verification reports are live-DB snapshots — not used for per-table
 * backup-vs-backup diffs. Presence (in dump TOC) is the comparable signal.
 */
import type { BackupVerificationReport } from '@/features/backup/logic/backupVerificationReportApi';

export type BackupDiffRow = {
  key: string;
  table: string;
  /** 1 = present in dump TOC, 0 = absent / not analyzed for that run. */
  count1: number;
  count2: number;
  /** count1 - count2 (−1, 0, or 1). */
  diff: number;
  onlyInBackup1: boolean;
  onlyInBackup2: boolean;
};

export type BackupDiffViewModel = {
  backup1Id: string;
  backup2Id: string;
  sizeBytes1: number;
  sizeBytes2: number;
  sizeDiffBytes: number;
  dump1Analyzed: boolean;
  dump2Analyzed: boolean;
  differences: BackupDiffRow[];
  /** Rows where presence differs between the two dumps. */
  changedCount: number;
};

function tableKey(schema: string | undefined, name: string): string {
  const s = (schema || 'public').trim() || 'public';
  return `${s}.${name}`;
}

function presenceMap(report: BackupVerificationReport | null | undefined): Map<string, boolean> {
  const map = new Map<string, boolean>();
  if (!report) return map;
  for (const row of report.tableStatistics ?? []) {
    const key = tableKey(row.schemaName, row.tableName);
    map.set(key, Boolean(row.presentInLogicalDump));
  }
  return map;
}

export function buildBackupDiffViewModel(
  report1: BackupVerificationReport | null | undefined,
  report2: BackupVerificationReport | null | undefined
): BackupDiffViewModel | null {
  if (!report1?.backupRunId || !report2?.backupRunId) return null;

  const map1 = presenceMap(report1);
  const map2 = presenceMap(report2);
  const keys = new Set<string>([...map1.keys(), ...map2.keys()]);

  const differences: BackupDiffRow[] = [...keys]
    .sort((a, b) => a.localeCompare(b))
    .map((key) => {
      const in1 = map1.get(key) === true;
      const in2 = map2.get(key) === true;
      const count1 = in1 ? 1 : 0;
      const count2 = in2 ? 1 : 0;
      return {
        key,
        table: key,
        count1,
        count2,
        diff: count1 - count2,
        onlyInBackup1: in1 && !in2,
        onlyInBackup2: in2 && !in1,
      };
    });

  const sizeBytes1 = report1.totalSizeBytes ?? 0;
  const sizeBytes2 = report2.totalSizeBytes ?? 0;

  return {
    backup1Id: report1.backupRunId,
    backup2Id: report2.backupRunId,
    sizeBytes1,
    sizeBytes2,
    sizeDiffBytes: sizeBytes1 - sizeBytes2,
    dump1Analyzed: report1.logicalDumpAnalyzed,
    dump2Analyzed: report2.logicalDumpAnalyzed,
    differences,
    changedCount: differences.filter((d) => d.diff !== 0).length,
  };
}

/** Prefer showing only rows that differ; caller can toggle. */
export function filterBackupDiffRows(rows: BackupDiffRow[], onlyChanged: boolean): BackupDiffRow[] {
  if (!onlyChanged) return rows;
  return rows.filter((r) => r.diff !== 0);
}
