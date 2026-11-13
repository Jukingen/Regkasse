import { describe, expect, it } from 'vitest';

import {
  buildBackupDiffViewModel,
  filterBackupDiffRows,
} from '@/features/backup/logic/backupDiffPresentation';
import type { BackupVerificationReport } from '@/features/backup/logic/backupVerificationReportApi';

function report(
  id: string,
  tables: { name: string; inDump: boolean }[],
  size: number
): BackupVerificationReport {
  return {
    backupRunId: id,
    generatedAtUtc: '2026-07-17T12:00:00Z',
    backupCompletedAtUtc: '2026-07-17T11:00:00Z',
    artifactCount: 1,
    totalSizeBytes: size,
    totalSizeFormatted: `${size}`,
    logicalDumpAnalyzed: true,
    logicalDumpAnalysisMessage: 'ok',
    tableStatistics: tables.map((t) => ({
      schemaName: 'public',
      tableName: t.name,
      rowCount: 10,
      estimatedSizeBytes: 100,
      presentInLogicalDump: t.inDump,
      isVerified: t.inDump,
      verificationMessage: null,
    })),
    sourceStatistics: null,
    verificationScore: 80,
    status: 'PartiallyVerified',
  };
}

describe('backupDiffPresentation', () => {
  it('computes presence diffs between two dumps', () => {
    const diff = buildBackupDiffViewModel(
      report(
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        [
          { name: 'products', inDump: true },
          { name: 'customers', inDump: true },
        ],
        1000
      ),
      report(
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        [
          { name: 'products', inDump: true },
          { name: 'customers', inDump: false },
          { name: 'vouchers', inDump: true },
        ],
        800
      )
    );

    expect(diff).not.toBeNull();
    expect(diff!.sizeDiffBytes).toBe(200);
    expect(diff!.changedCount).toBe(2);

    const byTable = Object.fromEntries(diff!.differences.map((r) => [r.table, r]));
    expect(byTable['public.products'].diff).toBe(0);
    expect(byTable['public.customers'].onlyInBackup1).toBe(true);
    expect(byTable['public.vouchers'].onlyInBackup2).toBe(true);
  });

  it('filters to changed rows only', () => {
    const diff = buildBackupDiffViewModel(
      report('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', [{ name: 'a', inDump: true }], 1),
      report(
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        [
          { name: 'a', inDump: true },
          { name: 'b', inDump: true },
        ],
        1
      )
    )!;
    expect(filterBackupDiffRows(diff.differences, true)).toHaveLength(1);
    expect(filterBackupDiffRows(diff.differences, false).length).toBeGreaterThan(1);
  });
});
