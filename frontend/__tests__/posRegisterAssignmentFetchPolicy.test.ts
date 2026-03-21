import { describe, expect, it } from '@jest/globals';

import { shouldFetchPosSelectableRegisterList } from '../utils/posRegisterAssignmentFetchPolicy';

const validId = '11111111-1111-1111-1111-111111111111';

describe('shouldFetchPosSelectableRegisterList', () => {
  const base = () => ({
    enabled: true,
    cashRegisterResolved: true,
    cashRegisterId: null as string | null,
    readinessNextAction: null as string | null,
    readinessEffectiveRegisterId: null as string | null,
    settingsLoadFailed: false,
    posEnsureReadyOnEntry: true,
    posReadinessLoading: false,
    posReadinessError: false,
  });

  it('readiness failed + settings failed: still fetch selectable (recovery path; empty list handled by caller UX)', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        settingsLoadFailed: true,
        posReadinessError: true,
        posReadinessLoading: false,
      })
    ).toBe(true);
  });

  it('readiness loading + settings failed: defer selectable until ensure-ready settles', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        settingsLoadFailed: true,
        posReadinessLoading: true,
        posReadinessError: false,
      })
    ).toBe(false);
  });

  it('readiness loading + settings failed + ensure-ready off: fetch immediately (no readiness gate)', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        settingsLoadFailed: true,
        posEnsureReadyOnEntry: false,
        posReadinessLoading: true,
        posReadinessError: false,
      })
    ).toBe(true);
  });

  it('settings ok + readiness still loading: still fetch in parallel (unchanged happy-path concurrency)', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        settingsLoadFailed: false,
        posReadinessLoading: true,
        posReadinessError: false,
      })
    ).toBe(true);
  });

  it('readiness ready: do not fetch selectable', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        readinessNextAction: 'ready',
        readinessEffectiveRegisterId: validId,
      })
    ).toBe(false);
  });

  it('valid cashRegisterId: do not fetch selectable', () => {
    expect(
      shouldFetchPosSelectableRegisterList({
        ...base(),
        cashRegisterId: validId,
      })
    ).toBe(false);
  });
});
