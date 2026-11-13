import {
  bytesToMegabytes,
  selectActiveArchivedExports,
  type DepExportArchiveReportDto,
} from '@/features/rksv/hooks/useDepExportArchive';

describe('useDepExportArchive helpers', () => {
  it('bytesToMegabytes rounds to two decimals', () => {
    expect(bytesToMegabytes(0)).toBe(0);
    expect(bytesToMegabytes(1024 * 1024)).toBe(1);
    expect(bytesToMegabytes(1.5 * 1024 * 1024)).toBe(1.5);
  });

  it('selectActiveArchivedExports filters purged and pending', () => {
    const report: DepExportArchiveReportDto = {
      tenantId: 't1',
      generatedAtUtc: new Date().toISOString(),
      totalCompletedExports: 3,
      archivedCount: 1,
      pendingArchiveCount: 1,
      purgedCount: 1,
      retentionYears: 7,
      totalArchivedSizeBytes: 100,
      recent: [
        {
          exportId: 'a',
          cashRegisterId: 'r',
          fileName: 'a.json',
          exportedAt: '2026-01-01T00:00:00Z',
          fileSizeBytes: 10,
          archivedAt: '2026-01-02T00:00:00Z',
          hasArchiveFile: true,
        },
        {
          exportId: 'b',
          cashRegisterId: 'r',
          fileName: 'b.json',
          exportedAt: '2026-01-01T00:00:00Z',
          fileSizeBytes: 10,
          archivedAt: null,
          hasArchiveFile: false,
        },
        {
          exportId: 'c',
          cashRegisterId: 'r',
          fileName: 'c.json',
          exportedAt: '2025-01-01T00:00:00Z',
          fileSizeBytes: 10,
          archivedAt: '2025-01-02T00:00:00Z',
          purgedAt: '2026-01-01T00:00:00Z',
          hasArchiveFile: false,
        },
      ],
    };

    const active = selectActiveArchivedExports(report);
    expect(active).toHaveLength(1);
    expect(active[0]?.exportId).toBe('a');
  });
});
