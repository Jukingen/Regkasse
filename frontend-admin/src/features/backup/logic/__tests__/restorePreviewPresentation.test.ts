import { describe, expect, it } from "vitest";
import type { BackupVerificationReport } from "@/features/backup/logic/backupVerificationReportApi";
import {
  mapVerificationReportToRestorePreview,
  restorePreviewSizeMib,
} from "@/features/backup/logic/restorePreviewPresentation";

function sampleReport(
  overrides?: Partial<BackupVerificationReport>,
): BackupVerificationReport {
  return {
    backupRunId: "run-1",
    generatedAtUtc: "2026-07-17T12:00:00Z",
    backupCompletedAtUtc: "2026-07-17T11:00:00Z",
    artifactCount: 2,
    totalSizeBytes: 5 * 1024 * 1024,
    totalSizeFormatted: "5.00 MB",
    logicalDumpAnalyzed: true,
    logicalDumpAnalysisMessage: "ok",
    tableStatistics: [
      {
        schemaName: "public",
        tableName: "products",
        rowCount: 100,
        estimatedSizeBytes: 1000,
        presentInLogicalDump: true,
        isVerified: true,
        verificationMessage: null,
      },
      {
        schemaName: "public",
        tableName: "customers",
        rowCount: 50,
        estimatedSizeBytes: 500,
        presentInLogicalDump: false,
        isVerified: false,
        verificationMessage: "missing",
      },
    ],
    sourceStatistics: {
      analyzedAtUtc: "2026-07-17T12:00:00Z",
      totalRowCount: 150,
      tables: [
        {
          schemaName: "public",
          tableName: "products",
          rowCount: 100,
          estimatedSizeBytes: 1000,
          tableExists: true,
        },
        {
          schemaName: "public",
          tableName: "customers",
          rowCount: 50,
          estimatedSizeBytes: 500,
          tableExists: true,
        },
      ],
    },
    verificationScore: 80,
    status: "PartiallyVerified",
    ...overrides,
  };
}

describe("restorePreviewPresentation", () => {
  it("maps dump-scoped tables when TOC was analyzed", () => {
    const preview = mapVerificationReportToRestorePreview(sampleReport());
    expect(preview).not.toBeNull();
    expect(preview!.tables).toBe(1);
    expect(preview!.records).toBe(100);
    expect(preview!.changes).toHaveLength(1);
    expect(preview!.changes[0].table).toBe("public.products");
    expect(preview!.changes[0].changeKind).toBe("aligned");
  });

  it("falls back to all monitored tables when dump not analyzed", () => {
    const preview = mapVerificationReportToRestorePreview(
      sampleReport({ logicalDumpAnalyzed: false }),
    );
    expect(preview!.tables).toBe(2);
    expect(preview!.records).toBe(150);
  });

  it("computes MiB for Descriptions", () => {
    expect(restorePreviewSizeMib(5 * 1024 * 1024)).toBe(5);
    expect(restorePreviewSizeMib(0)).toBe(0);
  });
});
