import { describe, expect, it } from 'vitest';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import {
  artifactByteSizeFootnoteKey,
  artifactClassLabelKeyForType,
  artifactContentExpectationKey,
  artifactRealityBadgeKey,
  buildArtifactDownloadRowTruth,
  contentExpectationTableSummaryKey,
  formatArtifactByteSize,
  sortArtifactsForOperatorDisplay,
  inferNonFakeArtifactSuspicion,
  inferRecoverabilityUse,
  inferSourceExecutionReality,
  shouldConfirmDownloadUnprovenLogicalDump,
  shouldOfferLastKnownGoodArtifactDownload,
} from '@/features/backup-dr/logic/backupArtifactDownloadTruth';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';

describe('shouldOfferLastKnownGoodArtifactDownload', () => {
  it('is true when latest failed and LKG id differs', () => {
    expect(
      shouldOfferLastKnownGoodArtifactDownload({
        latestRunId: 'fail-1',
        latestStatus: 4,
        lastSuccessfulBackupRunId: 'good-1',
      }),
    ).toBe(true);
  });

  it('is false when latest succeeded', () => {
    expect(
      shouldOfferLastKnownGoodArtifactDownload({
        latestRunId: 'a',
        latestStatus: 3,
        lastSuccessfulBackupRunId: 'good-1',
      }),
    ).toBe(false);
  });

  it('is false when LKG equals latest id', () => {
    expect(
      shouldOfferLastKnownGoodArtifactDownload({
        latestRunId: 'same',
        latestStatus: 4,
        lastSuccessfulBackupRunId: 'same',
      }),
    ).toBe(false);
  });
});

describe('formatArtifactByteSize', () => {
  const t = (k: string, o?: Record<string, string | number>) =>
    k + (o ? ` ${JSON.stringify(o)}` : '');

  it('formats small byte counts', () => {
    expect(formatArtifactByteSize(42, t)).toContain('42');
  });

  it('returns em dash when unknown', () => {
    expect(formatArtifactByteSize(undefined, t)).toBe('—');
  });
});

describe('buildArtifactDownloadRowTruth', () => {
  const baseArtifact = (over: Partial<BackupArtifactResponseDto>): BackupArtifactResponseDto => ({
    id: 'a1',
    artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
    isFilePresentForDownload: true,
    ...over,
  });

  it('blocks download when file presence unknown', () => {
    const row = buildArtifactDownloadRowTruth(baseArtifact({ isFilePresentForDownload: undefined }), {
      isSimulatedExecutionFlag: false,
      runAdapterKind: 'PgDump',
      realPostgreSqlLogicalDumpConfigured: true,
      canManage: true,
    });
    expect(row.download).toEqual({ state: 'blocked', reason: 'file_presence_unknown' });
  });

  it('blocks download when metadata exists but file reported absent on server', () => {
    const row = buildArtifactDownloadRowTruth(baseArtifact({ isFilePresentForDownload: false }), {
      isSimulatedExecutionFlag: false,
      runAdapterKind: 'PgDump',
      realPostgreSqlLogicalDumpConfigured: true,
      canManage: true,
    });
    expect(row.download).toEqual({ state: 'blocked', reason: 'file_not_on_server' });
    expect(row.filePresence).toBe('reported_absent');
  });

  it('uses stub label when simulated', () => {
    const row = buildArtifactDownloadRowTruth(baseArtifact({}), {
      isSimulatedExecutionFlag: true,
      runAdapterKind: 'Fake',
      realPostgreSqlLogicalDumpConfigured: false,
      canManage: true,
    });
    expect(row.artifactClassLabelKey).toBe('backupDr.download.types.logicalDumpStub');
    expect(row.contentExpectationKey).toBe('backupDr.download.contentExpect.stubLogicalDumpFakeAdapter');
    expect(row.recoverabilityUse).toBe('not_dr_evidence_simulated');
    expect(row.nonFakeSuspicion).toBe('none');
    expect(row.showIntegrityPrecheckDisclaimer).toBe(false);
  });

  it('non-simulated tiny logical dump keeps operational class label — not stub — and raises suspicion (not simulated_stub)', () => {
    const row = buildArtifactDownloadRowTruth(
      baseArtifact({ byteSize: 100, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 }),
      {
        isSimulatedExecutionFlag: false,
        runAdapterKind: 'PgDump',
        realPostgreSqlLogicalDumpConfigured: true,
        canManage: true,
      },
    );
    expect(row.sourceExecutionReality).toBe('non_simulated');
    expect(row.artifactClassLabelKey).toBe('backupDr.download.types.logicalDumpOperational');
    expect(row.nonFakeSuspicion).toBe('tiny_reported_logical_dump');
    expect(row.recoverabilityUse).not.toBe('not_dr_evidence_simulated');
  });

  it('uses manifest stub label for verification manifest when simulated', () => {
    const row = buildArtifactDownloadRowTruth(
      baseArtifact({ artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4 }),
      {
        isSimulatedExecutionFlag: true,
        runAdapterKind: 'Fake',
        realPostgreSqlLogicalDumpConfigured: false,
        canManage: true,
      },
    );
    expect(row.artifactClassLabelKey).toBe('backupDr.download.types.manifestStub');
    expect(row.contentExpectationKey).toBe('backupDr.download.contentExpect.stubManifestFakeAdapter');
    expect(row.recoverabilityUse).toBe('not_dr_evidence_simulated');
  });

  it('sorts logical dump before manifest like Fake adapter output', () => {
    const sorted = sortArtifactsForOperatorDisplay([
      { id: 'm', artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_4 },
      { id: 'l', artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
    ] as BackupArtifactResponseDto[]);
    expect(sorted.map((x) => x.id)).toEqual(['l', 'm']);
  });
});

describe('artifactContentExpectationKey', () => {
  it('matches Fake adapter logical + manifest keys', () => {
    expect(
      artifactContentExpectationKey(0, 'simulated_stub', false),
    ).toBe('backupDr.download.contentExpect.stubLogicalDumpFakeAdapter');
    expect(
      artifactContentExpectationKey(4, 'simulated_stub', false),
    ).toBe('backupDr.download.contentExpect.stubManifestFakeAdapter');
  });
});

describe('artifactClassLabelKeyForType', () => {
  it('uses not-proven when non-simulated but real pg not configured', () => {
    const k = artifactClassLabelKeyForType(0, 'non_simulated', false);
    expect(k).toBe('backupDr.download.types.logicalDumpNotProven');
  });

  it('uses operational when real pg configured and non-simulated', () => {
    const k = artifactClassLabelKeyForType(0, 'non_simulated', true);
    expect(k).toBe('backupDr.download.types.logicalDumpOperational');
  });

  it('uses stub-typed label for enum 1–3 in simulated_stub (not generic backup name)', () => {
    expect(artifactClassLabelKeyForType(1, 'simulated_stub', true)).toBe('backupDr.download.types.stub.1');
    expect(artifactClassLabelKeyForType(2, 'simulated_stub', true)).toBe('backupDr.download.types.stub.2');
    expect(artifactClassLabelKeyForType(3, 'simulated_stub', true)).toBe('backupDr.download.types.stub.3');
  });
});

describe('inferNonFakeArtifactSuspicion', () => {
  it('returns none for simulated_stub even if byte size missing', () => {
    expect(
      inferNonFakeArtifactSuspicion(
        { isFilePresentForDownload: true, byteSize: undefined, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
        'simulated_stub',
      ),
    ).toBe('none');
  });

  it('flags metadata_incomplete when file present but no byte size (non_simulated)', () => {
    expect(
      inferNonFakeArtifactSuspicion(
        { isFilePresentForDownload: true, byteSize: undefined, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
        'non_simulated',
      ),
    ).toBe('metadata_incomplete');
  });

  it('flags zero_reported_size when present and byte size 0', () => {
    expect(
      inferNonFakeArtifactSuspicion(
        { isFilePresentForDownload: true, byteSize: 0, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
        'non_simulated',
      ),
    ).toBe('zero_reported_size');
  });

  it('flags tiny logical dump when non_simulated and size below threshold', () => {
    expect(
      inferNonFakeArtifactSuspicion(
        { isFilePresentForDownload: true, byteSize: 100, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
        'non_simulated',
      ),
    ).toBe('tiny_reported_logical_dump');
  });

  it('does not flag tiny logical dump for unknown source (heuristic only for non_simulated)', () => {
    expect(
      inferNonFakeArtifactSuspicion(
        { isFilePresentForDownload: true, byteSize: 100, artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 },
        'unknown',
      ),
    ).toBe('none');
  });
});

describe('inferSourceExecutionReality / inferRecoverabilityUse', () => {
  it('marks unknown recovery when real pg flag missing', () => {
    const s = inferSourceExecutionReality({
      isSimulatedExecutionFlag: false,
      runAdapterKind: 'PgDump',
      realPostgreSqlLogicalDumpConfigured: undefined,
      canManage: true,
    });
    expect(s).toBe('non_simulated');
    expect(inferRecoverabilityUse(s, undefined)).toBe('unknown_recovery_value');
  });
});

describe('download UI helpers', () => {
  it('artifactRealityBadgeKey maps source', () => {
    expect(artifactRealityBadgeKey('simulated_stub')).toBe('backupDr.download.reality.stub');
    expect(artifactRealityBadgeKey('non_simulated')).toBe('backupDr.download.reality.realPipeline');
    expect(artifactRealityBadgeKey('unknown')).toBe('backupDr.download.reality.unknown');
  });

  it('artifactByteSizeFootnoteKey stub vs manifest', () => {
    expect(artifactByteSizeFootnoteKey(0, 'simulated_stub')).toBe('backupDr.download.byteSizeFootnote.stubExpectedTiny');
    expect(artifactByteSizeFootnoteKey(4, 'non_simulated')).toBe(
      'backupDr.download.byteSizeFootnote.manifestMetadataOnly',
    );
    expect(artifactByteSizeFootnoteKey(0, 'non_simulated')).toBeNull();
  });

  it('contentExpectationTableSummaryKey for common rows', () => {
    expect(
      contentExpectationTableSummaryKey(BackupArtifactResponseDtoArtifactType.NUMBER_0, 'simulated_stub', false),
    ).toBe('backupDr.download.contentExpectSummary.stubLogicalDumpFakeAdapter');
    expect(
      contentExpectationTableSummaryKey(BackupArtifactResponseDtoArtifactType.NUMBER_4, 'non_simulated', false),
    ).toBe('backupDr.download.contentExpectSummary.manifestNonStub');
  });

  it('shouldConfirmDownloadUnprovenLogicalDump only for non-sim logical dump + config', () => {
    const truth = buildArtifactDownloadRowTruth(
      {
        id: 'x',
        artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
        isFilePresentForDownload: true,
      },
      {
        isSimulatedExecutionFlag: false,
        runAdapterKind: 'PgDump',
        realPostgreSqlLogicalDumpConfigured: false,
        canManage: true,
      },
    );
    expect(
      shouldConfirmDownloadUnprovenLogicalDump(
        { artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0 } as never,
        truth,
      ),
    ).toBe(true);
  });
});
