import { describe, expect, it } from 'vitest';

import {
  FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT,
  PG_RESTORE_LIST_FAILED,
  interpretPgRestoreListFailure,
  parsePgRestoreListFailureContext,
  shouldShowFakeStubPgRestoreListExplainer,
} from '@/features/backup-dr/logic/restoreVerificationFailurePresentation';

describe('parsePgRestoreListFailureContext', () => {
  it('returns null for empty or invalid JSON', () => {
    expect(parsePgRestoreListFailureContext(null)).toBeNull();
    expect(parsePgRestoreListFailureContext('')).toBeNull();
    expect(parsePgRestoreListFailureContext('not json')).toBeNull();
  });

  it('reads pgRestoreListFailureContext from API detailsJson', () => {
    const j = JSON.stringify({
      pgRestoreListFailureContext: {
        reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT,
        sourceAdapterKind: 'Fake',
        sourceBackupRunId: 'run-1',
      },
    });
    expect(parsePgRestoreListFailureContext(j)).toEqual({
      reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT,
      sourceAdapterKind: 'Fake',
      sourceBackupRunId: 'run-1',
    });
  });
});

describe('shouldShowFakeStubPgRestoreListExplainer', () => {
  it('is false for other failure codes', () => {
    expect(
      shouldShowFakeStubPgRestoreListExplainer({
        failureCode: 'NO_DUMP_AVAILABLE',
        detailsJson: null,
        isSimulatedPipelineHeuristic: true,
      })
    ).toBe(false);
  });

  it('is true when DetailsJson marks Fake stub reason', () => {
    const detailsJson = JSON.stringify({
      pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
    });
    expect(
      shouldShowFakeStubPgRestoreListExplainer({
        failureCode: PG_RESTORE_LIST_FAILED,
        detailsJson,
        isSimulatedPipelineHeuristic: false,
      })
    ).toBe(true);
  });

  it('is true for PG_RESTORE_LIST_FAILED without DetailsJson when pipeline is simulated (heuristic)', () => {
    expect(
      shouldShowFakeStubPgRestoreListExplainer({
        failureCode: PG_RESTORE_LIST_FAILED,
        detailsJson: null,
        isSimulatedPipelineHeuristic: true,
      })
    ).toBe(true);
  });

  it('is false for PG_RESTORE_LIST_FAILED without DetailsJson when pipeline is assumed real', () => {
    expect(
      shouldShowFakeStubPgRestoreListExplainer({
        failureCode: PG_RESTORE_LIST_FAILED,
        detailsJson: null,
        isSimulatedPipelineHeuristic: false,
      })
    ).toBe(false);
  });
});

describe('interpretPgRestoreListFailure', () => {
  const base = {
    failureCode: PG_RESTORE_LIST_FAILED,
    failureDetail: '',
    detailsJson: null as string | null,
    pgRestoreListExitCode: null as number | null,
    dumpInspectionPassed: false as boolean | null,
  };

  it('classifies Fake stub context as fake_stub_expected even when heuristic is false', () => {
    const r = interpretPgRestoreListFailure({
      run: {
        ...base,
        detailsJson: JSON.stringify({
          pgRestoreListFailureContext: { reason: FAKE_ADAPTER_STUB_NOT_PG_RESTORE_FORMAT },
        }),
      },
      isSimulatedPipelineHeuristic: false,
    });
    expect(r?.kind).toBe('fake_stub_expected');
  });

  it('classifies exit -1 + Failed to start as tooling', () => {
    const r = interpretPgRestoreListFailure({
      run: {
        ...base,
        pgRestoreListExitCode: -1,
        failureDetail: 'Failed to start pg_restore.',
      },
      isSimulatedPipelineHeuristic: false,
    });
    expect(r?.kind).toBe('real_pg_restore_unavailable');
  });

  it('classifies exit -1 + Dump file missing', () => {
    const r = interpretPgRestoreListFailure({
      run: {
        ...base,
        pgRestoreListExitCode: -1,
        failureDetail: 'Dump file missing.',
      },
      isSimulatedPipelineHeuristic: false,
    });
    expect(r?.kind).toBe('real_dump_file_missing');
  });

  it('classifies non-zero exit as format/corrupt', () => {
    const r = interpretPgRestoreListFailure({
      run: { ...base, pgRestoreListExitCode: 1, failureDetail: 'stderr' },
      isSimulatedPipelineHeuristic: false,
    });
    expect(r?.kind).toBe('real_format_or_corrupt');
  });
});
